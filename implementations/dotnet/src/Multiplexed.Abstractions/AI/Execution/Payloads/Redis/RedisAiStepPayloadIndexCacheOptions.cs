namespace Multiplexed.Abstractions.AI.Execution.Payloads.Redis
{
    /// <summary>
    /// Redis cache options for archived step payload index entries.
    /// </summary>
    public sealed class RedisAiStepPayloadIndexCacheOptions
    {
        public bool Enabled { get; set; } = true;

        public string KeyPrefix { get; set; } = "ai:step-payload-index";

        public int ExpirationSeconds { get; set; } = 3600;

        public bool RefreshTtlOnRead { get; set; } = true;
    }
}