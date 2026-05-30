using Multiplexed.Abstractions.AI.Observability.Metrics.Retention;
using System;

namespace Multiplexed.AI.Runtime.Observability.Metrics.Retention
{
    /// <summary>
    /// Default implementation of <see cref="IAiRetentionMetrics"/>.
    ///
    /// PURPOSE:
    /// - Acts as a structured container for all retention metrics domains.
    /// - Ensures all retention stages are available and properly wired.
    ///
    /// IMPORTANT:
    /// - This class does not implement metrics logic itself.
    /// - It only delegates to specialized sub-metrics components.
    /// </summary>
    public sealed class AiRetentionMetrics : IAiRetentionMetrics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRetentionMetrics"/> class.
        /// </summary>
        /// <param name="trigger">Trigger metrics.</param>
        /// <param name="decision">Decision metrics.</param>
        /// <param name="plan">Plan metrics.</param>
        /// <param name="execution">Execution metrics.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any dependency is null.
        /// </exception>
        public AiRetentionMetrics(
            IAiRetentionTriggerMetrics trigger,
            IAiRetentionDecisionMetrics decision,
            IAiRetentionPlanMetrics plan,
            IAiRetentionExecutionMetrics execution)
        {
            Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Execution = execution ?? throw new ArgumentNullException(nameof(execution));
        }

        /// <inheritdoc />
        public IAiRetentionTriggerMetrics Trigger { get; }

        /// <inheritdoc />
        public IAiRetentionDecisionMetrics Decision { get; }

        /// <inheritdoc />
        public IAiRetentionPlanMetrics Plan { get; }

        /// <inheritdoc />
        public IAiRetentionExecutionMetrics Execution { get; }
    }
}