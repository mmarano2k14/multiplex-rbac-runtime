namespace Multiplexed.AI.Runtime.Configuration
{
    /// <summary>
    /// Controls automatic cleanup behavior for AI executions.
    /// </summary>
    public sealed class AiExecutionCleanupOptions
    {
        /// <summary>
        /// Automatically cleanup completed executions.
        /// </summary>
        public bool AutoCleanupOnCompleted { get; set; }

        /// <summary>
        /// Automatically cleanup failed executions.
        /// </summary>
        public bool AutoCleanupOnFailed { get; set; }

        /// <summary>
        /// If true, cleanup failures are swallowed after being logged.
        /// If false, cleanup failures are rethrown.
        /// </summary>
        public bool SuppressCleanupExceptions { get; set; } = true;

        /// <summary>
        /// If true, cleanup persistent snaphashot.
        /// If false, snapshot will remain in the provider.
        /// </summary>
        public bool SuppressSnapshotIfExist{ get; set; } = false;
    }
}