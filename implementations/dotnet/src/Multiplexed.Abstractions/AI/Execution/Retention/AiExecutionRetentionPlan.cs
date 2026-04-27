using System;
using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Execution.Retention
{
    /// <summary>
    /// Represents a retention decision computed by a policy.
    ///
    /// PURPOSE:
    /// - Describe which steps should be compacted or evicted
    /// - Decouple decision from execution
    ///
    /// IMPORTANT:
    /// - This is a pure data structure (no logic)
    /// - It does NOT mutate the execution state
    /// - It is consumed by a retention service
    ///
    /// DESIGN:
    /// - Compact and Evict are independent
    /// - Hybrid mode populates both lists
    /// </summary>
    public sealed class AiExecutionRetentionPlan
    {
        /// <summary>
        /// Steps that should be compacted.
        ///
        /// The step remains in the state,
        /// but heavy payload is moved externally.
        /// </summary>
        public IReadOnlyList<string> StepsToCompact { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Steps that should be evicted from the state.
        ///
        /// These steps must already exist in the payload store
        /// before removal.
        /// </summary>
        public IReadOnlyList<string> StepsToEvict { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Indicates whether this plan contains any work.
        /// </summary>
        public bool HasWork =>
            StepsToCompact.Count > 0 ||
            StepsToEvict.Count > 0;
    }
}