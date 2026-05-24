using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Observability.Context;

namespace Multiplexed.AI.Runtime.Observability.Helpers
{
    /// <summary>
    /// Provides helper methods for creating runtime correlation contexts used by
    /// tracing, metrics, decision ledger recording, and future replay diagnostics.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Centralizes creation of <see cref="AiRuntimeCorrelationContext"/>.
    /// - Avoids duplicating correlation mapping across runtime services.
    /// - Keeps observability correlation independent from the observability facade.
    ///
    /// IMPORTANT:
    /// - This helper does not record metrics.
    /// - This helper does not write traces.
    /// - This helper does not append ledger entries.
    /// - It only builds correlation data.
    /// </remarks>
    internal static class AiRuntimeCorrelationContextHelper
    {
        /// <summary>
        /// Creates a runtime correlation context from execution, step, worker,
        /// claim, and optional concurrency information.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="pipelineKey">The stable pipeline key.</param>
        /// <param name="stepName">The DAG step name.</param>
        /// <param name="workerId">The worker or runtime instance identifier.</param>
        /// <param name="claimToken">The optional distributed claim token.</param>
        /// <param name="concurrencyContext">The optional concurrency context.</param>
        /// <returns>The runtime correlation context.</returns>
        public static AiRuntimeCorrelationContext Create(
            string executionId,
            string pipelineKey,
            string stepName,
            string workerId,
            string? claimToken = null,
            AiConcurrencyContext? concurrencyContext = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

            return new AiRuntimeCorrelationContext
            {
                ExecutionId = executionId,
                PipelineName = pipelineKey,
                StepId = stepName,
                StepKey = concurrencyContext?.StepKey ?? stepName,
                RuntimeInstanceId = concurrencyContext?.RuntimeInstanceId ?? workerId,
                WorkerId = workerId,
                ClaimToken = claimToken,
                Provider = concurrencyContext?.Provider,
                Model = concurrencyContext?.Model,
                Operation = concurrencyContext?.Operation,
                CorrelationId = executionId
            };
        }
    }
}