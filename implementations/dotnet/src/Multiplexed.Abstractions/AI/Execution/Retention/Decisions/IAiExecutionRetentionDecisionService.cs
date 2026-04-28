using System.Collections.Generic;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;

namespace Multiplexed.Abstractions.AI.Execution.Retention.Decisions
{
    /// <summary>
    /// Provides decision support for execution retention.
    ///
    /// PURPOSE:
    /// - Decide whether retention should run.
    /// - Enrich an existing retention plan with per-step decisions.
    /// - Keep adaptive trigger and decision evaluation outside the retention application service.
    ///
    /// IMPORTANT:
    /// - Must not apply retention.
    /// - Must not mutate execution state.
    /// - Must not perform payload persistence or eviction.
    /// </summary>
    public interface IAiExecutionRetentionDecisionService
    {
        /// <summary>
        /// Determines whether retention should run for the specified state.
        /// </summary>
        AiExecutionRetentionDecisionServiceResult Evaluate(
            AiExecutionState state);

        /// <summary>
        /// Enriches retention candidates using per-step decision policies.
        /// </summary>
        void EnrichPlan(
            AiExecutionState state,
            ISet<string> stepsToCompact,
            ISet<string> stepsToEvict,
            AiExecutionRetentionTriggerContext triggerContext);
    }
}