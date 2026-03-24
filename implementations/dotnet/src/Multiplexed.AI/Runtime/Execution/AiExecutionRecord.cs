using System;
using System.Collections.Generic;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Represents the persisted state of an AI execution pipeline.
    /// 
    /// This object is designed to be stored in a durable store (e.g. Redis, DB)
    /// and allows safe continuation, replay, and inspection of execution.
    /// 
    /// It acts as the single source of truth for:
    /// - current step progression
    /// - RBAC context key (rotated between steps)
    /// - execution metadata and shared state
    /// </summary>
    public sealed class AiExecutionRecord
    {
        /// <summary>
        /// Unique identifier of the execution.
        /// Used for correlation, logging, and retrieval.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Current RBAC ContextKey used to resolve the ExecutionContext from the store.
        /// 
        /// IMPORTANT:
        /// - This key is rotated after each step.
        /// - This is the ONLY source of truth to retrieve the context.
        /// </summary>
        public string ContextKey { get; set; } = string.Empty;

        /// <summary>
        /// Index of the current step in the execution pipeline.
        /// </summary>
        public int CurrentStepIndex { get; set; }

        /// <summary>
        /// Ordered list of step type names (AssemblyQualifiedName).
        /// </summary>
        public List<string> Steps { get; set; } = new();

        /// <summary>
        /// Shared execution data between steps.
        /// 
        /// This can be used to pass intermediate results across steps.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Execution metadata for diagnostics, tracing, and orchestration.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// List of completed steps (by name) in execution order.
        /// </summary>
        public List<string> CompletedSteps { get; set; } = new();

        /// <summary>
        /// Snapshot of the RBAC execution context captured at pipeline creation time.
        /// 
        /// IMPORTANT:
        /// - This is NOT used for execution.
        /// - Only used for debugging, traceability, or recovery strategies.
        /// </summary>
        public ExecutionContextSnapshot? ExecutionContextSnapshot { get; set; }

        /// <summary>
        /// Current execution status (Pending, Running, Completed, Failed).
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Incremented on each successful state transition.
        /// Used for optimistic concurrency (future V3).
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// UTC timestamp of creation.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp of last update.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Name of the current step being executed.
        /// </summary>
        public string CurrentStep { get; set; } = String.Empty;

        /// <summary>
        ///Key  Name of the current step being executed.
        /// </summary>
        public string ExecutionStepKey { get; set; } = Guid.NewGuid().ToString("N");
    }
}