using Multiplexed.Abstractions.Core.ExecutionContext;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the persisted orchestration record of an AI execution.
    ///
    /// This object is the durable source of truth for workflow progression and lifecycle state.
    /// It intentionally does not contain the mutable execution payload exchanged between steps.
    ///
    /// Responsibilities:
    /// - identify the execution
    /// - track the current step and execution progression
    /// - store durable orchestration metadata
    /// - support optimistic concurrency during step transitions
    ///
    /// Important:
    /// - Mutable execution data is stored separately in <see cref="AiExecutionState"/>.
    /// - This record should remain focused on orchestration concerns only.
    /// </summary>
    public sealed class AiExecutionRecord
    {
        /// <summary>
        /// Gets or sets the unique identifier of the execution.
        /// Used for correlation, retrieval, logging, and association with execution state.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the current RBAC context key used to resolve the live execution context.
        ///
        /// Important:
        /// - This key may rotate after each successful step.
        /// - This is the active runtime key used by the RBAC context store.
        /// </summary>
        public string ContextKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the zero-based index of the current step in the execution pipeline.
        /// </summary>
        public int CurrentStepIndex { get; set; }

        /// <summary>
        /// Gets or sets the ordered list of pipeline step identifiers.
        ///
        /// Recommended:
        /// - Use stable logical step names or type identifiers.
        /// - Avoid volatile names that may break replay or resumption behavior.
        /// </summary>
        public List<string> Steps { get; set; } = new();

        /// <summary>
        /// Gets or sets the ordered list of successfully completed step names.
        /// This list reflects historical progression and may be used for recovery or diagnostics.
        /// </summary>
        public List<string> CompletedSteps { get; set; } = new();

        /// <summary>
        /// Gets or sets the RBAC execution context snapshot captured when the AI pipeline was created.
        ///
        /// Important:
        /// - This snapshot is not the live execution context.
        /// - It is retained for traceability, auditing, and recovery scenarios.
        /// </summary>
        public ExecutionContextSnapshot? ExecutionContextSnapshot { get; set; }

        /// <summary>
        /// Gets or sets the current execution lifecycle status.
        ///
        /// Typical values:
        /// - Pending
        /// - Running
        /// - Completed
        /// - Failed
        ///
        /// Note:
        /// A dedicated enum can be introduced later if stronger typing is required.
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Gets or sets the optimistic concurrency version.
        /// Incremented on each successful orchestration transition.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the name of the current step being executed.
        /// This is typically aligned with one of the entries in <see cref="Steps"/>.
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the execution-level step transition key used for optimistic concurrency.
        ///
        /// Important:
        /// - This is separate from <see cref="ContextKey"/>.
        /// - <see cref="ContextKey"/> protects RBAC execution context access.
        /// - <see cref="ExecutionStepKey"/> protects AI execution progression updates.
        /// </summary>
        public string ExecutionStepKey { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the UTC timestamp when the execution record was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp when the execution record was last updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Returns true when the execution has reached a terminal state.
        /// </summary>
        public bool IsTerminal =>
            string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, "Failed", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns true when the execution is currently in progress.
        /// </summary>
        public bool IsRunning =>
            string.Equals(Status, "Running", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Marks the execution as running and updates the last modification timestamp.
        /// </summary>
        public void MarkRunning()
        {
            Status = "Running";
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the execution as completed and updates the last modification timestamp.
        /// </summary>
        public void MarkCompleted()
        {
            Status = "Completed";
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the execution as failed and updates the last modification timestamp.
        /// </summary>
        public void MarkFailed()
        {
            Status = "Failed";
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Advances the optimistic concurrency version and updates the last modification timestamp.
        /// </summary>
        public void TouchVersion()
        {
            Version++;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Replaces the current execution step transition key and updates the last modification timestamp.
        /// </summary>
        public void RenewExecutionStepKey()
        {
            ExecutionStepKey = Guid.NewGuid().ToString("N");
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}