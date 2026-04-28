using System;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.Abstractions.AI.Execution.Retention.Decisions;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;

namespace Multiplexed.AI.Runtime.Retention.Decisions
{
    /// <summary>
    /// Default composite implementation of <see cref="IAiExecutionRetentionDecisionEvaluator"/>.
    ///
    /// PURPOSE:
    /// - Execute all registered retention decision policies.
    /// - Combine policy recommendations into one deterministic final decision.
    ///
    /// BEHAVIOR:
    /// - Evaluates policies in registration order.
    /// - Selects the strongest action using deterministic action priority.
    /// - Preserves the reason from the policy that produced the selected action.
    ///
    /// IMPORTANT:
    /// - Does not mutate execution state.
    /// - Does not perform I/O.
    /// - Must remain deterministic.
    /// </summary>
    public sealed class CompositeAiExecutionRetentionDecisionEvaluator : IAiExecutionRetentionDecisionEvaluator
    {
        private readonly IReadOnlyList<IAiExecutionRetentionDecisionPolicy> _policies;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAiExecutionRetentionDecisionEvaluator"/> class.
        /// </summary>
        /// <param name="policies">The registered retention decision policies.</param>
        public CompositeAiExecutionRetentionDecisionEvaluator(
            IEnumerable<IAiExecutionRetentionDecisionPolicy> policies)
        {
            _policies = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        }

        /// <inheritdoc />
        public AiExecutionRetentionDecision Evaluate(AiExecutionRetentionDecisionContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (_policies.Count == 0)
            {
                return new AiExecutionRetentionDecision
                {
                    Action = AiExecutionRetentionAction.Keep,
                    Reason = "no_policies_registered"
                };
            }

            var finalDecision = new AiExecutionRetentionDecision
            {
                Action = AiExecutionRetentionAction.Keep,
                Reason = "default_keep"
            };

            foreach (var policy in _policies)
            {
                var decision = policy.Evaluate(context);

                if (decision is null)
                {
                    continue;
                }

                if (GetPriority(decision.Action) > GetPriority(finalDecision.Action))
                {
                    finalDecision = decision;
                }
            }

            return finalDecision;
        }

        /// <summary>
        /// Gets the deterministic priority associated with a retention action.
        /// </summary>
        private static int GetPriority(AiExecutionRetentionAction action)
        {
            return action switch
            {
                AiExecutionRetentionAction.Evict => 400,
                AiExecutionRetentionAction.Compact => 300,
                AiExecutionRetentionAction.Keep => 200,
                AiExecutionRetentionAction.None => 100,
                _ => 0
            };
        }
    }
}