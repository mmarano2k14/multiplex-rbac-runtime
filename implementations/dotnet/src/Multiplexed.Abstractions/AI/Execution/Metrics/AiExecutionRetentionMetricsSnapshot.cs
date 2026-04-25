namespace Multiplexed.Abstractions.AI.Execution.Metrics
{
    /// <summary>
    /// Represents a snapshot of retention metrics.
    /// </summary>
    public sealed class AiExecutionRetentionMetricsSnapshot
    {
        public int TotalStepsBefore { get; init; }
        public int TotalStepsAfter { get; init; }
        public int EvictedSteps { get; init; }
        public int ActiveSteps { get; init; }
        public int PendingSteps { get; init; }
    }
}