using System;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;

namespace Multiplexed.AI.Runtime.Retention.Decisions.Policies
{
    /// <summary>
    /// Retention decision policy based on step payload size.
    ///
    /// PURPOSE:
    /// - Detect large inline payloads.
    /// - Recommend compaction for oversized step results.
    ///
    /// BEHAVIOR:
    /// - If payload exceeds threshold → Compact
    /// - Otherwise → Keep
    ///
    /// IMPORTANT:
    /// - Does not mutate execution state.
    /// - Does not perform I/O.
    /// - Deterministic.
    /// </summary>
    public sealed class SizeBasedAiExecutionRetentionDecisionPolicy : IAiExecutionRetentionDecisionPolicy
    {
        private readonly long _maxInlineBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="SizeBasedAiExecutionRetentionDecisionPolicy"/> class.
        /// </summary>
        /// <param name="maxInlineBytes">Maximum allowed inline payload size in bytes.</param>
        public SizeBasedAiExecutionRetentionDecisionPolicy(long maxInlineBytes)
        {
            _maxInlineBytes = maxInlineBytes;
        }

        /// <inheritdoc />
        public AiExecutionRetentionDecision Evaluate(AiExecutionRetentionDecisionContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (context.StepInlinePayloadBytes > _maxInlineBytes)
            {
                return new AiExecutionRetentionDecision
                {
                    Action = AiExecutionRetentionAction.Compact,
                    Reason = "payload_too_large"
                };
            }

            return new AiExecutionRetentionDecision
            {
                Action = AiExecutionRetentionAction.Keep,
                Reason = "within_size_limit"
            };
        }
    }
}