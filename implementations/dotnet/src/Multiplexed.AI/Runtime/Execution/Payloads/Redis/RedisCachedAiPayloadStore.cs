using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using StackExchange.Redis;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Redis
{
    /// <summary>
    /// Redis-cached payload store layered on top of a durable payload store (Mongo).
    ///
    /// PURPOSE:
    /// - Provides fast access to frequently used payloads
    /// - Reduces MongoDB reads during active execution
    /// - Keeps Redis usage bounded via size limits and expiration
    ///
    /// DESIGN:
    /// - Mongo remains the source of truth
    /// - Redis acts as a read-through/write-through cache
    /// - Cache population is opportunistic and size-aware
    ///
    /// IMPORTANT:
    /// - Redis is NOT required for replay
    /// - Cache misses must fallback to Mongo
    /// - Cache writes must never fail execution
    /// </summary>
    public sealed class RedisCachedAiPayloadStore : IAiPayloadStore
    {
        private readonly IAiPayloadStore _innerStore;
        private readonly IConnectionMultiplexer _redis;
        private readonly RedisAiPayloadCacheOptions _options;

        public RedisCachedAiPayloadStore(
            IAiPayloadStore innerStore,
            IConnectionMultiplexer redis,
            IOptions<AiPayloadStoreOptions> options)
        {
            ArgumentNullException.ThrowIfNull(innerStore);
            ArgumentNullException.ThrowIfNull(redis);
            ArgumentNullException.ThrowIfNull(options);

            _innerStore = innerStore;
            _redis = redis;
            _options = options.Value.RedisCache;
        }

        public async Task<string> SaveAsync(
            string content,
            CancellationToken cancellationToken = default)
        {
            // Always persist to Mongo first (durable)
            var id = await _innerStore.SaveAsync(content, cancellationToken);

            // Cache only if eligible
            TryCache(id, content);

            return id;
        }

        public async Task<string?> LoadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            var redisKey = BuildKey(key);

            // Try Redis first
            var cached = await db.StringGetAsync(redisKey);

            if (cached.HasValue)
            {
                return cached!;
            }

            // Fallback to Mongo
            var content = await _innerStore.LoadAsync(key, cancellationToken);

            if (content is null)
                return null;

            // Re-cache if eligible
            TryCache(key, content);

            return content;
        }

        public async Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();

            // Remove cache
            await db.KeyDeleteAsync(BuildKey(key));

            // Remove from Mongo
            await _innerStore.DeleteAsync(key, cancellationToken);
        }

        /// <summary>
        /// Attempts to cache payload in Redis if it satisfies size constraints.
        /// </summary>
        private void TryCache(string key, string content)
        {
            if (!_options.Enabled)
                return;

            if (content.Length > _options.MaxCacheablePayloadBytes)
                return;

            try
            {
                var db = _redis.GetDatabase();

                db.StringSet(
                    BuildKey(key),
                    content,
                    TimeSpan.FromSeconds(_options.ExpirationSeconds));
            }
            catch
            {
                // Cache is best-effort only
            }
        }

        private string BuildKey(string key)
        {
            return $"{_options.KeyPrefix}:{key}";
        }
    }
}