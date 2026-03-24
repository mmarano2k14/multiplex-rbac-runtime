using System;
using System.Collections.Generic;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Represents the persisted state of an AI execution pipeline.
    /// 
    /// This object is designed to be stored in a durable store and acts as the
    /// orchestration source of truth for step progression and execution lifecycle.
    /// 
    /// IMPORTANT:
    /// - Execution payload state is stored separately in AiExecutionState.
    /// - This record tracks orchestration only.
    /// </summary>
    public sealed class AiExecutionRecord
    {
        /// <summary>
        /// Unique identifier of the execution.
        /// Used for correlation, logging, retrieval, and state association.
        /// </summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Current RBAC ContextKey used to resolve the live ExecutionContext from the store.
        /// 
        /// IMPORTANT:
        /// - This key is rotated after each step.
        /// - This is the only valid key for RBAC execution at runtime.
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
        /// List of completed steps in execution order.
        /// </summary>
        public List<string> CompletedSteps { get; set; } = new();

        /// <summary>
        /// Snapshot of the RBAC execution context captured at pipeline creation time.
        /// 
        /// IMPORTANT:
        /// - This is not used for execution.
        /// - It is kept for traceability and recovery strategies.
        /// </summary>
        public ExecutionContextSnapshot? ExecutionContextSnapshot { get; set; }

        /// <summary>
        /// Current execution status (Pending, Running, Completed, Failed).
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Incremented on each successful state transition.
        /// Used for optimistic concurrency.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Name of the current step being executed.
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// Execution-level step key used for optimistic concurrency between step transitions.
        /// 
        /// IMPORTANT:
        /// - This is different from ContextKey.
        /// - ContextKey protects RBAC execution.
        /// - ExecutionStepKey protects AI execution progression.
        /// </summary>
        public string ExecutionStepKey { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// UTC timestamp of creation.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp of last update.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}