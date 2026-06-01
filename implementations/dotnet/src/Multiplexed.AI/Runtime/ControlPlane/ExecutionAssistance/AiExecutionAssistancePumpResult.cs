using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;

namespace Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents the result of an execution assistance pump operation.
    /// </summary>
    public sealed class AiExecutionAssistancePumpResult
    {
        /// <summary>
        /// Gets a value indicating whether the pump operation succeeded.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the assistance lease identifier.
        /// </summary>
        public string? LeaseId { get; init; }

        /// <summary>
        /// Gets the execution identifier that was assisted.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Gets the helper runtime instance identifier.
        /// </summary>
        public string? HelperRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the final assistance lease status.
        /// </summary>
        public AiExecutionAssistanceStatus? Status { get; init; }

        /// <summary>
        /// Gets the number of helper workers started by the pump.
        /// </summary>
        public int StartedWorkerCount { get; init; }

        /// <summary>
        /// Gets the failure reason when the pump operation failed.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the pump operation started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; init; } =
            DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the UTC timestamp when the pump operation completed.
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; init; } =
            DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets additional metadata associated with the pump result.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}