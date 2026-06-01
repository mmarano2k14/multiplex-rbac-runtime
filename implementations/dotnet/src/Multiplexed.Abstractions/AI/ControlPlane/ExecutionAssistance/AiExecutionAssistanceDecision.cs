namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents the decision produced when evaluating whether a runtime instance
    /// may assist an active execution.
    /// </summary>
    public sealed class AiExecutionAssistanceDecision
    {
        /// <summary>
        /// Gets a value indicating whether assistance is allowed.
        /// </summary>
        public bool Allowed { get; init; }

        /// <summary>
        /// Gets the execution identifier evaluated for assistance.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the primary runtime instance that owns the execution lifecycle.
        /// </summary>
        public required string PrimaryRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the candidate helper runtime instance.
        /// </summary>
        public required string HelperRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the assistance lease when assistance is allowed.
        /// </summary>
        public AiExecutionAssistanceLease? Lease { get; init; }

        /// <summary>
        /// Gets the reason explaining why assistance was allowed or denied.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the failure reason when evaluation failed.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the decision.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}