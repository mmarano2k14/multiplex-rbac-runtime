namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents a request to evaluate whether a runtime instance can assist
    /// an active execution owned by another runtime instance.
    /// </summary>
    public sealed class AiExecutionAssistanceRequest
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
        /// Gets the candidate helper runtime instance.
        /// </summary>
        public required string HelperRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the number of ready steps currently available for execution.
        /// </summary>
        public int ReadyStepCount { get; init; }

        /// <summary>
        /// Gets the number of pending or non-terminal steps remaining in the execution.
        /// </summary>
        public int RemainingStepCount { get; init; }

        /// <summary>
        /// Gets the number of active helper leases already assigned to this execution.
        /// </summary>
        public int ActiveHelperCount { get; init; }

        /// <summary>
        /// Gets the number of active workers already assisting or executing this execution.
        /// </summary>
        public int ActiveWorkerCountForExecution { get; init; }

        /// <summary>
        /// Gets a value indicating whether the helper runtime instance is currently idle.
        /// </summary>
        public bool HelperIsIdle { get; init; }

        /// <summary>
        /// Gets the local queue depth of the helper runtime instance.
        /// </summary>
        public int HelperQueueDepth { get; init; }

        /// <summary>
        /// Gets the available worker slot count on the helper runtime instance.
        /// </summary>
        public int HelperAvailableWorkerSlots { get; init; }

        /// <summary>
        /// Gets the correlation identifier used for observability.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Gets the user, component, or system that requested assistance evaluation.
        /// </summary>
        public string? RequestedBy { get; init; }

        /// <summary>
        /// Gets the source component that requested assistance evaluation.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// Gets the reason for evaluating assistance.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the request.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}