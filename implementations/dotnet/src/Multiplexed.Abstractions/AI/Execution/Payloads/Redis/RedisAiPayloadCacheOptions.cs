namespace Multiplexed.Abstractions.AI.Execution.Payloads.Redis
{
    /// <summary>
    /// Configures Redis as a bounded cache for execution payloads.
    ///
    /// PURPOSE:
    /// - Improves read performance for hot payloads.
    /// - Reduces repeated MongoDB reads during active execution.
    /// - Keeps Redis memory bounded through size limits and expiration.
    ///
    /// IMPORTANT:
    /// - Redis is not the replay-safe source of truth.
    /// - Redis cache entries may expire.
    /// - MongoDB must remain the durable backing store when replay safety is required.
    /// </summary>
    public sealed class RedisAiPayloadCacheOptions
    {
        /// <summary>
        /// Gets or sets whether Redis payload caching is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the Redis key prefix for cached payload content.
        /// </summary>
        public string KeyPrefix { get; set; } = "ai:payload";

        /// <summary>
        /// Gets or sets the maximum payload size eligible for Redis caching.
        ///
        /// Payloads larger than this value are stored only in MongoDB.
        /// </summary>
        public int MaxCacheablePayloadBytes { get; set; } = 256 * 1024;

        /// <summary>
        /// Gets or sets the Redis cache expiration in seconds.
        /// </summary>
        public int ExpirationSeconds { get; set; } = 3600;
    }
}