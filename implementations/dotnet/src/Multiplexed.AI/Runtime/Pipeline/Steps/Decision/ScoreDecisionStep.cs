using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Decision
{
    /// <summary>
    /// Deterministic decision step based on a numeric score.
    ///
    /// PURPOSE:
    /// - Converts upstream AI output into a stable business decision
    /// - Enables downstream routing without requiring another LLM call
    ///
    /// DESIGN:
    /// - Uses the shared declared variable bag from the execution context
    /// - Lets the execution context resolve and convert the requested variable
    /// - Applies deterministic business rules on top of the resolved value
    ///
    /// INPUT:
    /// - score
    ///
    /// OUTPUT:
    /// - shortlist
    /// - review
    /// - reject
    ///
    /// CONFIG:
    /// - shortlistThreshold (default: 80)
    /// - rejectThreshold (default: 50)
    /// </summary>
    [AiStep("decision.score")]
    public sealed class ScoreDecisionStep : IAiStep
    {
        public string Name => "decision.score";

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var score = await helper.GetRequiredInputAsync<int>(
                "score",
                cancellationToken).ConfigureAwait(false);

            var shortlistThreshold = (await helper.GetConfigAsync<int?>(
                 "shortlistThreshold",
                 cancellationToken).ConfigureAwait(false)) ?? 80;

            var rejectThreshold = (await helper.GetConfigAsync<int?>(
                "rejectThreshold",
                cancellationToken).ConfigureAwait(false)) ?? 50;

            if (rejectThreshold > shortlistThreshold)
            {
                throw new InvalidOperationException(
                    $"Invalid decision thresholds. rejectThreshold '{rejectThreshold}' cannot be greater than shortlistThreshold '{shortlistThreshold}'.");
            }

            string decision;

            if (score >= shortlistThreshold)
            {
                decision = "shortlist";
            }
            else if (score <= rejectThreshold)
            {
                decision = "reject";
            }
            else
            {
                decision = "review";
            }

            return AiStepResult.Ok(
                output: decision,
                value: decision,
                data: helper.ToDictionary(new
                {
                    decision,
                    score,
                    shortlistThreshold,
                    rejectThreshold
                }, ignoreNull: true));
        }
    }
}