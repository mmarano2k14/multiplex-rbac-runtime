using Multiplexed.Abstractions.Core.ExecutionContext;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the persisted orchestration record of an AI execution.
    ///
    /// This object is the authoritative source for the global execution lifecycle.
    /// It stores orchestration metadata such as:
    ///
    /// - Execution identity
    /// - Pipeline binding
    /// - Current step position
    /// - Global lifecycle status
    /// - Concurrency/versioning information
    ///
    /// It is intentionally separated from <see cref="AiExecutionState"/>
    /// which stores the mutable working payload exchanged between steps.
    /// </summary>
    public sealed class AiExecutionRecord
    {
        /// <summary>
        /// Gets or sets the unique execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the optional pipeline name associated with this execution.
        /// This value identifies which declarative workflow definition should be used
        /// when continuing or replaying the execution.
        /// </summary>
        public string? PipelineName { get; set; }

        /// <summary>
        /// Gets or sets the current RBAC context key used to resolve the live execution context.
        /// </summary>
        public string ContextKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the zero-based index of the current step.
        /// </summary>
        public int CurrentStepIndex { get; set; }

        /// <summary>
        /// Gets or sets the ordered list of configured step names.
        /// </summary>
        public List<string> Steps { get; set; } = new();

        /// <summary>
        /// Gets or sets the ordered list of completed step names.
        /// </summary>
        public List<string> CompletedSteps { get; set; } = new();

        /// <summary>
        /// Gets or sets the RBAC execution context snapshot captured at creation time.
        /// </summary>
        public ExecutionContextSnapshot? ExecutionContextSnapshot { get; set; }

        /// <summary>
        /// Gets or sets the current global execution lifecycle status.
        /// This is the authoritative orchestration status for the execution as a whole.
        /// </summary>
        public AiExecutionStatus Status { get; set; } = AiExecutionStatus.Pending;

        /// <summary>
        /// Gets or sets the optimistic concurrency version.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the current step name.
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the execution-level step transition key.
        /// This key can be renewed after each successful step transition
        /// to support optimistic concurrency and duplicate transition protection.
        /// </summary>
        public string ExecutionStepKey { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the UTC creation timestamp.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC last update timestamp.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Returns true when the execution is in a terminal state.
        /// Terminal states cannot transition to any other status.
        /// </summary>
        public bool IsTerminal =>
            Status is AiExecutionStatus.Completed
                or AiExecutionStatus.Failed
                or AiExecutionStatus.Cancelled;

        /// <summary>
        /// Returns true when the execution is currently running.
        /// </summary>
        public bool IsRunning => Status == AiExecutionStatus.Running;

        /// <summary>
        /// Returns true when the execution is currently waiting.
        /// </summary>
        public bool IsWaiting => Status == AiExecutionStatus.Waiting;

        /// <summary>
        /// Marks the execution as running.
        /// </summary>
        public void MarkRunning()
        {
            Status = AiExecutionStatus.Running;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the execution as waiting.
        /// Use this when the execution as a whole is suspended
        /// pending an external event or resumable condition.
        /// </summary>
        public void MarkWaiting()
        {
            Status = AiExecutionStatus.Waiting;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the execution as completed.
        /// </summary>
        public void MarkCompleted()
        {
            Status = AiExecutionStatus.Completed;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the execution as failed.
        /// </summary>
        public void MarkFailed()
        {
            Status = AiExecutionStatus.Failed;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the execution as cancelled.
        /// </summary>
        public void MarkCancelled()
        {
            Status = AiExecutionStatus.Cancelled;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Increments the orchestration version.
        /// This should be called whenever the persisted orchestration record
        /// changes in a way that matters for optimistic concurrency.
        /// </summary>
        public void TouchVersion()
        {
            Version++;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Replaces the execution step key.
        /// This should typically be renewed after a successful step transition
        /// so that stale concurrent writers cannot replay the same transition.
        /// </summary>
        public void RenewExecutionStepKey()
        {
            ExecutionStepKey = Guid.NewGuid().ToString("N");
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}