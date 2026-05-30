using Multiplexed.AI.Abstractions.AI.Policies;
using System;

namespace Multiplexed.Abstractions.AI.Observability.Metrics.Policy
{
    /// <summary>
    /// Defines metrics recording for AI policy execution.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Track policy execution frequency and outcomes.
    /// - Provide visibility into policy performance and reliability.
    ///
    /// DESIGN:
    /// - This interface is write-only from the runtime perspective.
    /// - Implementations are responsible for aggregation, storage, and export.
    ///
    /// IMPORTANT:
    /// - Metrics must never influence policy execution or decision logic.
    /// </remarks>
    public interface IAiPolicyMetrics
    {
        /// <summary>
        /// Records a policy execution event.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="policyName">The name of the executed policy.</param>
        /// <param name="success">Indicates whether the policy execution succeeded.</param>
        /// <param name="duration">The execution duration.</param>
        void RecordExecution(
            string executionId,
            string policyName,
            bool success,
            TimeSpan duration);

        /// <summary>
        /// Records a policy failure event.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="policyName">The name of the failed policy.</param>
        void RecordFailure(
            string executionId,
            string policyName);

        /// <summary>
        /// Records a policy decision outcome.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="policyName">The name of the policy that produced the decision.</param>
        /// <param name="kind">The policy result kind representing the decision outcome.</param>
        /// <remarks>
        /// This method tracks decision-level outcomes such as Retry or Block,
        /// which are distinct from execution success or failure.
        /// </remarks>
        void RecordDecision(
            string executionId,
            string policyName,
            AiPolicyResultKind kind);
    }
}