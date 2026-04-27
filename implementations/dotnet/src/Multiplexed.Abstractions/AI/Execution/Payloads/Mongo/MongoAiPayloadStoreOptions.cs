namespace Multiplexed.Abstractions.AI.Execution.Payloads.Mongo
{
    /// <summary>
    /// Configures MongoDB-backed payload storage.
    ///
    /// PURPOSE:
    /// - Stores externalized execution payloads durably.
    /// - Keeps large payloads outside the execution state and snapshots.
    /// - Makes replay and recovery possible after process restart.
    ///
    /// IMPORTANT:
    /// - MongoDB is the recommended source of truth for replay-safe payloads.
    /// - Payload documents should live at least as long as their related execution snapshots.
    /// </summary>
    public sealed class MongoAiPayloadStoreOptions
    {
        /// <summary>
        /// Gets or sets whether MongoDB payload storage is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the MongoDB connection string.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the MongoDB database name.
        /// </summary>
        public string? DatabaseName { get; set; }

        /// <summary>
        /// Gets or sets the MongoDB collection name used for payload documents.
        /// </summary>
        public string CollectionName { get; set; } = "ai_execution_payloads";
    }
}