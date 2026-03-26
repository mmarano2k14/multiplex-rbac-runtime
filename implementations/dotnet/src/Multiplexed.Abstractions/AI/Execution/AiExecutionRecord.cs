using Multiplexed.Abstractions.Core.ExecutionContext;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the persisted orchestration record of an AI execution.
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
        /// Gets or sets the current execution lifecycle status.
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
        /// Marks the execution as running.
        /// </summary>
        public void MarkRunning()
        {
            Status = AiExecutionStatus.Running;
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
        /// </summary>
        public void TouchVersion()
        {
            Version++;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Replaces the execution step key.
        /// </summary>
        public void RenewExecutionStepKey()
        {
            ExecutionStepKey = Guid.NewGuid().ToString("N");
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}