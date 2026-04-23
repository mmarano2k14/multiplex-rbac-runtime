namespace Multiplexed.Abstractions.AI.Execution.Cleanup
{
    /// <summary>
    /// Deletes distributed DAG step persistence and associated claim metadata
    /// for a given execution.
    /// </summary>
    public interface IAiDagDistributedStateCleanup
    {
        Task<AiDagDistributedCleanupResult> DeleteDistributedStateAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }

    public sealed class AiDagDistributedCleanupResult
    {
        public int StepCountDiscovered { get; init; }
        public int StepCountDeleted { get; init; }

        public bool ClaimsFound { get; init; }
        public bool ClaimsDeleted { get; init; }

        public bool InFlightFound { get; init; }
        public bool InFlightDeleted { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }
}