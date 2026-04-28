using System;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Triggers;

namespace Multiplexed.AI.Runtime.Retention.Triggers
{
    /// <summary>
    /// Default threshold-based implementation of <see cref="IAiExecutionRetentionTrigger"/>.
    ///
    /// PURPOSE:
    /// - Trigger retention when execution state pressure exceeds configured thresholds.
    /// - Avoid executing retention unnecessarily.
    ///
    /// IMPORTANT:
    /// - Does not mutate execution state.
    /// - Does not perform I/O.
    /// - Must remain deterministic and fast.
    /// </summary>
    public sealed class DefaultAiExecutionRetentionTrigger : IAiExecutionRetentionTrigger
    {
        private readonly AiExecutionRetentionTriggerOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionRetentionTrigger"/> class.
        /// </summary>
        /// <param name="options">The retention trigger options.</param>
        public DefaultAiExecutionRetentionTrigger(AiExecutionRetentionTriggerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public bool ShouldRun(AiExecutionRetentionTriggerContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return context.TotalStepsCount > _options.MaxStepsInState
                   || context.CompletedStepsCount > _options.MaxCompletedStepsInState
                   || context.EstimatedInlinePayloadBytes > _options.MaxInlinePayloadBytes;
        }
    }
}