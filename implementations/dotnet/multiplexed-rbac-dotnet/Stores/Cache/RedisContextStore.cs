using MultiplexedRbac.Core.ExecutionContext;
using StackExchange.Redis;
using System.Text.Json;

namespace MultiplexedRbac.Stores.Cache
{
    public sealed class RedisContextStore : IContextStore
    {
        private readonly IDatabase _db;
        private readonly JsonSerializerOptions _jsonOptions;

        private const string ContextPrefix = "ac:ctx:";
        private const string InFlightPrefix = "ac:inflight:";

        private const int DefaultTtlSeconds = 900;        // 15 minutes
        private const int RotationOverlapSeconds = 10;    // overlap window
        private const int InFlightExtraSeconds = 5;       // safety margin

        public RedisContextStore(IConnectionMultiplexer multiplexer)
        {
            _db = multiplexer.GetDatabase();
            _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        }

        private string ContextKey(string key) => $"{ContextPrefix}{key}";
        private string InFlightKey(string key) => $"{InFlightPrefix}{key}";

        private static string GenerateNewKey()
            => "ctx_" + Guid.NewGuid().ToString("N");

        // ------------------------------------------------------------
        // Store
        // ------------------------------------------------------------
        public async Task<string> StoreAsync(Core.ExecutionContext.ExecutionContext context)
        {
            var key = GenerateNewKey();
            context.ContextKey = key;
            var json = JsonSerializer.Serialize(context, _jsonOptions);
            

            await _db.StringSetAsync(
                ContextKey(key),
                json,
                TimeSpan.FromSeconds(DefaultTtlSeconds));

            return key;
        }

        // ------------------------------------------------------------
        // Seed just in case forcing context with no dynamic key
        // ------------------------------------------------------------
        public async Task<string> SeedAsync(Core.ExecutionContext.ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(context.ContextKey))
                throw new ArgumentException("ContextKey must be set for seeding.", nameof(context));

            var key = context.ContextKey;
            var json = JsonSerializer.Serialize(context, _jsonOptions);

            await _db.StringSetAsync(
                ContextKey(key), // => "ac:ctx:" + key
                json,
                TimeSpan.FromSeconds(DefaultTtlSeconds));

            return key; // returns logical key (what clients send in X-Access-Context)
        }

        // ------------------------------------------------------------
        // Get
        // ------------------------------------------------------------
        public async Task<Core.ExecutionContext.ExecutionContext?> GetAsync(string key)
        {
            var value = await _db.StringGetAsync(ContextKey(key));
            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<Core.ExecutionContext.ExecutionContext>(value!, _jsonOptions);
        }

        // ------------------------------------------------------------
        // Acquire In-Flight (Lua atomic)
        // ------------------------------------------------------------

        private const string AcquireScript = @"
            local ctxKey = KEYS[1]
            local inKey = KEYS[2]
            local extra = tonumber(ARGV[1])
            local defaultTtl = tonumber(ARGV[2])

            if redis.call('EXISTS', ctxKey) == 0 then
              return 0
            end

            local ttl = redis.call('TTL', ctxKey)
            if ttl < 0 then
              ttl = defaultTtl
            end

            local n = redis.call('INCR', inKey)
            redis.call('EXPIRE', inKey, ttl + extra)
            return n
            ";

        public async Task<bool> TryAcquireInFlightAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var rr = await _db.ScriptEvaluateAsync(
                AcquireScript,
                new RedisKey[] { ContextKey(key), InFlightKey(key) },
                new RedisValue[] { (RedisValue)InFlightExtraSeconds, (RedisValue)DefaultTtlSeconds });

            // RedisResult can be Int64; parse safely
            var n = (long)rr;
            return n > 0;
        }

        // ------------------------------------------------------------
        // Release In-Flight (Lua atomic)
        // ------------------------------------------------------------
        private static readonly LuaScript ReleaseScript = LuaScript.Prepare(@"
            local inKey = KEYS[1]
            if redis.call('EXISTS', inKey) == 0 then
                return 0
            end

            local n = redis.call('DECR', inKey)
            if n <= 0 then
                redis.call('DEL', inKey)
            end

            return n
        ");

        public async Task ReleaseInFlightAsync(string key)
        {
            await _db.ScriptEvaluateAsync(
                ReleaseScript,
                new RedisKey[] { InFlightKey(key) });
        }

        // ------------------------------------------------------------
        // Rotation (Lua overlap-safe)
        // ------------------------------------------------------------

        private const string RotateScript = @"
            local oldKey = KEYS[1]
            local newKey = KEYS[2]
            local newTtl = tonumber(ARGV[1])
            local overlap = tonumber(ARGV[2])

            local value = redis.call('GET', oldKey)
            if not value then
              return nil
            end

            redis.call('SET', newKey, value, 'EX', newTtl)

            local oldTtl = redis.call('TTL', oldKey)
            if oldTtl < 0 then
              redis.call('EXPIRE', oldKey, overlap)
            elseif oldTtl > overlap then
              redis.call('EXPIRE', oldKey, overlap)
            end

            return value
            ";

        public async Task<(string newKey, Core.ExecutionContext.ExecutionContext context)> RotateAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null/empty.", nameof(key));

            var newKey = GenerateNewKey();

            // IMPORTANT:
            // - KEYS are the real Redis keys (with prefix)
            // - ARGV are numbers (TTL seconds)
            var result = await _db.ScriptEvaluateAsync(
                RotateScript,
                keys: new RedisKey[] { ContextKey(key), ContextKey(newKey) },
                values: new RedisValue[] { DefaultTtlSeconds, RotationOverlapSeconds });

            if (result.IsNull)
                throw new KeyNotFoundException("Cannot rotate: context not found or expired.");

            var json = (string)result!;
            var ctx = JsonSerializer.Deserialize<Core.ExecutionContext.ExecutionContext>(json, _jsonOptions)
                      ?? throw new InvalidOperationException("Invalid context JSON in Redis.");

            // Optional but consistent with your middleware behavior:
            ctx.ContextKey = key;

            return (newKey, ctx);
        }
    }
}
