using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

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

        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            // Compose once, then reuse.
            _ = context.ResolveDeclaredInputs();

            var score = context.GetRequiredVariable<int>("score");

            var shortlistThreshold = context.TryGetStepConfigValue<int>("shortlistThreshold", out var shortlist)
                ? shortlist
                : 80;

            var rejectThreshold = context.TryGetStepConfigValue<int>("rejectThreshold", out var reject)
                ? reject
                : 50;

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

            return Task.FromResult(
                AiStepResult.Ok(
                    output: decision,
                    value: decision,
                    data: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["decision"] = decision,
                        ["score"] = score,
                        ["shortlistThreshold"] = shortlistThreshold,
                        ["rejectThreshold"] = rejectThreshold
                    }));
        }
    }
}