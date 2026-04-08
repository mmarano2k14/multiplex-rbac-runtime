namespace Multiplexed.AI.Configuration
{
    /// <summary>
    /// Controls durable execution snapshot persistence.
    /// </summary>
    public sealed class AiExecutionSnapshotOptions
    {
        /// <summary>
        /// Enables durable execution snapshot persistence.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets MongoDB-specific snapshot options.
        /// </summary>
        public AiExecutionSnapshotMongoOptions Mongo { get; set; } = new();
    }
}