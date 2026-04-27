using System;

namespace Multiplexed.Abstractions.AI.Execution.Retention
{
    /// <summary>
    /// Defines how execution steps should be retained in memory.
    ///
    /// PURPOSE:
    /// - Control memory usage of AiExecutionState
    /// - Enable externalization of step data
    /// - Support long-running DAG executions
    ///
    /// IMPORTANT:
    /// - This enum does NOT perform any logic by itself
    /// - It is interpreted by a retention policy + service
    ///
    /// MODES:
    /// - None: no retention applied
    /// - Compact: keep step inline, externalize heavy payload
    /// - Evict: remove step from state, keep full payload externally
    /// - Hybrid: Compact + Evict (safe pipeline)
    /// </summary>
    public enum AiExecutionRetentionMode
    {
        /// <summary>
        /// No retention is applied.
        /// All steps remain in the state.
        /// </summary>
        None = 0,

        /// <summary>
        /// Keeps the step in the state but moves heavy data
        /// (e.g., Result.Data) to an external payload store.
        ///
        /// Safe default mode.
        /// </summary>
        Compact = 1,

        /// <summary>
        /// Removes the step from the state.
        ///
        /// The full step must be available in the payload store
        /// before eviction.
        /// </summary>
        Evict = 2,

        /// <summary>
        /// Combines Compact + Evict.
        ///
        /// Flow:
        /// 1. Compact payload (ensure externalization)
        /// 2. Evict step from state
        ///
        /// Recommended for long-running or large DAGs.
        /// </summary>
        Hybrid = 3
    }
}