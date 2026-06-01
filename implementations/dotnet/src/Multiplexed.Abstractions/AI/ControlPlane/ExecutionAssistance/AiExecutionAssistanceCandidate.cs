namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents an active execution that may receive cross-instance assistance.
    /// </summary>
    /// <remarks>
    /// A candidate is registered by the primary runtime instance that owns the
    /// execution lifecycle. Helper runtime instances may later evaluate this
    /// candidate and request assistance leases when they are idle and the execution
    /// is under-provisioned.
    /// </remarks>
    public sealed class AiExecutionAssistanceCandidate
    {
        /// <summary>
        /// Gets the execution identifier that may receive assistance.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the primary runtime instance that owns the execution lifecycle.
        /// </summary>
        public required string PrimaryRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the local runtime run identifier associated with the execution.
        /// </summary>
        public string? LocalRunId { get; init; }

        /// <summary>
        /// Gets the pipeline name associated with the execution.
        /// </summary>
        public required string PipelineName { get; init; }

        /// <summary>
        /// Gets the pipeline version associated with the execution, when available.
        /// </summary>
        public string? PipelineVersion { get; init; }

        /// <summary>
        /// Gets the estimated number of ready steps available for assistance.
        /// </summary>
        public int EstimatedReadyStepCount { get; init; }

        /// <summary>
        /// Gets the estimated number of remaining non-terminal steps.
        /// </summary>
        public int EstimatedRemainingStepCount { get; init; }

        /// <summary>
        /// Gets the estimated number of active workers currently assigned to the execution.
        /// </summary>
        public int EstimatedActiveWorkerCount { get; init; }

        /// <summary>
        /// Gets a value indicating whether the candidate is currently active.
        /// </summary>
        public bool IsActive { get; init; } = true;

        /// <summary>
        /// Gets the UTC timestamp when the candidate was registered.
        /// </summary>
        public DateTimeOffset RegisteredAtUtc { get; init; } =
            DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the UTC timestamp when the candidate was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAtUtc { get; init; } =
            DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the UTC timestamp when the candidate completed, failed, or was cancelled.
        /// </summary>
        public DateTimeOffset? CompletedAtUtc { get; init; }

        /// <summary>
        /// Gets the completion or removal reason.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the assistance candidate.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}