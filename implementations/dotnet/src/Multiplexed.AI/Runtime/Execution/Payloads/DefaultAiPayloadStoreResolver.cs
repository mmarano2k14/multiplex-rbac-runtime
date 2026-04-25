using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Default payload store resolver.
    ///
    /// PURPOSE:
    /// - Selects the active payload store based on <see cref="AiPayloadStoreOptions"/>.
    /// - Keeps execution data policy independent from concrete storage providers.
    ///
    /// SUPPORTED PROVIDERS:
    /// - inmemory
    /// - mongo
    /// - redis
    /// - mongo-redis
    ///
    /// IMPORTANT:
    /// - In-memory is not replay-safe after process restart.
    /// - Redis-only is not replay-safe after eviction, expiration or flush.
    /// - Mongo is the recommended durable provider.
    /// - Mongo-Redis uses Mongo as source of truth and Redis as bounded cache.
    /// </summary>
    public sealed class DefaultAiPayloadStoreResolver : IAiPayloadStoreResolver
    {
        private readonly IServiceProvider _services;
        private readonly AiPayloadStoreOptions _options;

        public DefaultAiPayloadStoreResolver(
            IServiceProvider services,
            IOptions<AiPayloadStoreOptions> options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            _services = services;
            _options = options.Value;
        }

        /// <inheritdoc />
        public IAiPayloadStore Resolve()
        {
            if (!_options.Enabled)
            {
                return _services.GetRequiredService<InMemoryAiPayloadStore>();
            }

            var provider = (_options.Provider ?? "inmemory").Trim().ToLowerInvariant();

            if (_options.RequireReplaySafePayloads &&
                (provider == "inmemory" || provider == "redis"))
            {
                throw new InvalidOperationException(
                    $"Replay-safe payloads are required, but the configured payload store provider is '{provider}'. " +
                    "Use 'mongo' or 'mongo-redis' for replay-safe payload storage.");
            }

            return provider switch
            {
                "mongo" => _services.GetRequiredService<MongoAiPayloadStore>(),

                "redis" => _services.GetRequiredService<RedisAiPayloadStore>(),

                "mongo-redis" => _services.GetRequiredService<MongoRedisCachedAiPayloadStore>(),

                "inmemory" => _services.GetRequiredService<InMemoryAiPayloadStore>(),

                _ => throw new InvalidOperationException(
                    $"Unsupported AI payload store provider '{_options.Provider}'. " +
                    "Supported providers are: inmemory, mongo, redis, mongo-redis.")
            };
        }
    }
}