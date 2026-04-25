namespace Multiplexed.Abstractions.AI.Execution.Metrics
{
    /// <summary>
    /// Captures execution state retention metrics.
    ///
    /// PURPOSE:
    /// - Provides visibility into how execution state evolves after retention.
    /// - Helps monitor memory pressure and eviction behavior.
    ///
    /// IMPORTANT:
    /// - Metrics are observational only and must not affect execution behavior.
    /// </summary>
    public interface IAiExecutionRetentionMetrics
    {
        /// <summary>
        /// Records retention activity for a given execution snapshot.
        /// </summary>
        /// <param name="totalStepsBefore">Total number of steps before retention.</param>
        /// <param name="totalStepsAfter">Total number of steps after retention.</param>
        /// <param name="completedEvicted">Number of completed steps evicted.</param>
        /// <param name="activeSteps">Number of active steps (Running).</param>
        /// <param name="pendingSteps">Number of pending steps (Ready/WaitingForRetry).</param>
        void RecordRetention(
            int totalStepsBefore,
            int totalStepsAfter,
            int completedEvicted,
            int activeSteps,
            int pendingSteps);
    }
}