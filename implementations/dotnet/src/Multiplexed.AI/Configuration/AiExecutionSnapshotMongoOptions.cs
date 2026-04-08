namespace Multiplexed.AI.Configuration
{
    /// <summary>
    /// Controls MongoDB persistence settings for AI execution snapshots.
    /// </summary>
    public sealed class AiExecutionSnapshotMongoOptions
    {
        /// <summary>
        /// Enables MongoDB as the snapshot persistence provider.
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
        /// Gets or sets the MongoDB collection name used for execution snapshots.
        /// </summary>
        public string CollectionName { get; set; } = "ai_execution_snapshots";
    }
}