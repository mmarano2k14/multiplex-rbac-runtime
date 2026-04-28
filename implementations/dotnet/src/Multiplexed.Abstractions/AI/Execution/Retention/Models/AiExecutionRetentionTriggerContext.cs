namespace Multiplexed.Abstractions.AI.Execution.Retention.Models
{
    /// <summary>
    /// Provides lightweight metrics about the execution state used to determine
    /// whether retention should be executed.
    ///
    /// PURPOSE:
    /// - Expose minimal, aggregated state information
    /// - Avoid passing heavy objects or large payloads
    ///
    /// IMPORTANT:
    /// - Immutable snapshot
    /// - No direct references to execution state internals
    /// </summary>
    public sealed class AiExecutionRetentionTriggerContext
    {
        /// <summary>
        /// Gets the total number of steps currently present in the execution state.
        /// </summary>
        public int TotalStepsCount { get; init; }

        /// <summary>
        /// Gets the number of completed steps currently present in the execution state.
        /// </summary>
        public int CompletedStepsCount { get; init; }

        /// <summary>
        /// Gets the estimated size (in bytes) of inline payload data stored in the execution state.
        /// </summary>
        public long EstimatedInlinePayloadBytes { get; init; }
    }
}