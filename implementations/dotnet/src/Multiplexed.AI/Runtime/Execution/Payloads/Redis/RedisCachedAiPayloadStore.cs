using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using StackExchange.Redis;
using System.Text;

namespace Multiplexed.AI.Runtime.Execution.Payloads.Redis
{
    /// <summary>
    /// Redis cache decorator for an existing durable payload store.
    ///
    /// PURPOSE:
    /// - Adds Redis read-through/write-through cache behavior on top of another payload store.
    /// - Reduces reads against the durable source of truth during active executions.
    /// - Keeps Redis bounded using TTL and max cacheable payload size.
    ///
    /// DESIGN:
    /// - The wrapped store remains the source of truth.
    /// - Redis is used only as an opportunistic cache.
    /// - Cache failures must never fail execution.
    ///
    /// IMPORTANT:
    /// - This class is a decorator, not a standalone provider.
    /// - For Mongo + Redis, use <see cref="MongoRedisCachedAiPayloadStore"/>.
    /// </summary>
    public sealed class RedisCachedAiPayloadStore : IAiPayloadStore
    {
        private readonly IAiPayloadStore _innerStore;
        private readonly IConnectionMultiplexer _redis;
        private readonly RedisAiPayloadCacheOptions _options;
        private readonly IAiPayloadMetrics _payloadMetrics;

        public RedisCachedAiPayloadStore(
            IAiPayloadStore innerStore,
            IConnectionMultiplexer redis,
            IOptions<AiPayloadStoreOptions> options,
            IAiPayloadMetrics payloadMetrics)
        {
            ArgumentNullException.ThrowIfNull(innerStore);
            ArgumentNullException.ThrowIfNull(redis);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(payloadMetrics);

            _innerStore = innerStore;
            _redis = redis;
            _options = options.Value.RedisCache;
            _payloadMetrics = payloadMetrics;
        }

        /// <inheritdoc />
        public async Task<string> SaveAsync(
            string content,
            CancellationToken cancellationToken = default)
        {
            var id = await _innerStore.SaveAsync(content, cancellationToken)
                .ConfigureAwait(false);

            await TryCacheAsync(id, content).ConfigureAwait(false);

            return id;
        }

        /// <inheritdoc />
        public async Task<string?> LoadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            var redisKey = BuildKey(key);

            try
            {
                var db = _redis.GetDatabase();

                var cached = await db.StringGetAsync(redisKey)
                    .ConfigureAwait(false);

                if (cached.HasValue)
                {
                    _payloadMetrics.RecordCacheHit();
                    return cached.ToString();
                }

                _payloadMetrics.RecordCacheMiss();
            }
            catch
            {
                _payloadMetrics.RecordCacheMiss();
            }

            _payloadMetrics.RecordCacheFallback();

            var content = await _innerStore.LoadAsync(key, cancellationToken)
                .ConfigureAwait(false);

            if (content is null)
            {
                return null;
            }

            await TryCacheAsync(key, content).ConfigureAwait(false);

            return content;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();

                await db.KeyDeleteAsync(BuildKey(key))
                    .ConfigureAwait(false);
            }
            catch
            {
                // Cache delete is best-effort only.
            }

            await _innerStore.DeleteAsync(key, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to cache payload content when Redis cache is enabled and size-eligible.
        ///
        /// IMPORTANT:
        /// - Cache writes are best-effort.
        /// - Failures are swallowed intentionally because the durable store already contains the payload.
        /// </summary>
        private async Task TryCacheAsync(string key, string content)
        {
            if (!_options.Enabled)
            {
                return;
            }

            var sizeBytes = Encoding.UTF8.GetByteCount(content);

            if (sizeBytes > _options.MaxCacheablePayloadBytes)
            {
                return;
            }

            try
            {
                var db = _redis.GetDatabase();

                await db.StringSetAsync(
                        BuildKey(key),
                        content,
                        TimeSpan.FromSeconds(_options.ExpirationSeconds))
                    .ConfigureAwait(false);

                _payloadMetrics.RecordCacheWrite();
            }
            catch
            {
                // Cache write is best-effort only.
            }
        }

        /// <summary>
        /// Builds the Redis key used for payload cache entries.
        /// </summary>
        private string BuildKey(string key)
        {
            return $"{_options.KeyPrefix}:{key}";
        }
    }
}