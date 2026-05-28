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
    /// - Centralizes creation of <see cref="AiRuntimeLedgerEventCorrelationContext"/>.
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
        /// <param name="stepName">
        /// The logical DAG step name. In the correlation model this maps to <c>StepId</c>.
        /// </param>
        /// <param name="workerId">The worker or runtime instance identifier.</param>
        /// <param name="claimToken">The optional distributed claim token.</param>
        /// <param name="concurrencyContext">
        /// The optional concurrency context used to enrich step key, provider, model,
        /// operation, and runtime instance correlation.
        /// </param>
        /// <param name="runId">
        /// The optional controller run identifier associated with this runtime event.
        /// </param>
        /// <param name="correlationId">
        /// The optional external or controller-level correlation identifier.
        /// </param>
        /// <returns>The runtime correlation context.</returns>
        /// <remarks>
        /// This overload is kept for backward compatibility.
        /// When no explicit technical step key is supplied, the helper falls back to
        /// <paramref name="stepName"/> unless the concurrency context provides a step key.
        /// </remarks>
        public static AiRuntimeLedgerEventCorrelationContext Create(
            string executionId,
            string pipelineKey,
            string stepName,
            string workerId,
            string? claimToken = null,
            AiConcurrencyContext? concurrencyContext = null,
            string? runId = null,
            string? correlationId = null)
        {
            var resolvedStepKey = !string.IsNullOrWhiteSpace(concurrencyContext?.StepKey)
                ? concurrencyContext.StepKey
                : stepName;

            return Create(
                executionId,
                pipelineKey,
                stepName,
                resolvedStepKey,
                workerId,
                claimToken,
                concurrencyContext,
                runId,
                correlationId);
        }

        /// <summary>
        /// Creates a runtime correlation context from execution, logical step identity,
        /// technical step key, worker, claim, and optional concurrency information.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="pipelineKey">The stable pipeline key.</param>
        /// <param name="stepName">
        /// The logical DAG step name. In the correlation model this maps to <c>StepId</c>.
        /// Example: <c>chaos-step-001</c>.
        /// </param>
        /// <param name="stepKey">
        /// The technical implementation/type key. In the correlation model this maps to <c>StepKey</c>.
        /// Example: <c>hello-world</c> or <c>distributed.chaos.flaky-provider</c>.
        /// </param>
        /// <param name="workerId">The worker or runtime instance identifier.</param>
        /// <param name="claimToken">The optional distributed claim token.</param>
        /// <param name="concurrencyContext">
        /// The optional concurrency context used to enrich provider, model, operation,
        /// runtime instance, and optionally override the technical step key when present.
        /// </param>
        /// <param name="runId">
        /// The optional controller run identifier associated with this runtime event.
        /// </param>
        /// <param name="correlationId">
        /// The optional external or controller-level correlation identifier.
        /// </param>
        /// <returns>The runtime correlation context.</returns>
        /// <remarks>
        /// <para>
        /// <c>StepId</c> is the logical DAG step identity.
        /// </para>
        /// <para>
        /// <c>StepKey</c> is the runtime implementation/type key.
        /// </para>
        /// <para>
        /// <c>RunId</c> is separate from <c>ExecutionId</c>. Controller run and queue
        /// events may use a durable execution identifier while still preserving the
        /// controller run identifier for observability and replay diagnostics.
        /// </para>
        /// <para>
        /// This overload should be preferred when the caller knows both the logical
        /// DAG step name and the technical step key.
        /// </para>
        /// </remarks>
        public static AiRuntimeLedgerEventCorrelationContext Create(
            string executionId,
            string pipelineKey,
            string stepName,
            string stepKey,
            string workerId,
            string? claimToken = null,
            AiConcurrencyContext? concurrencyContext = null,
            string? runId = null,
            string? correlationId = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

            var resolvedStepKey = !string.IsNullOrWhiteSpace(concurrencyContext?.StepKey)
                ? concurrencyContext.StepKey
                : stepKey;

            var resolvedRuntimeInstanceId = !string.IsNullOrWhiteSpace(concurrencyContext?.RuntimeInstanceId)
                ? concurrencyContext.RuntimeInstanceId
                : workerId;

            var resolvedCorrelationId = !string.IsNullOrWhiteSpace(correlationId)
                ? correlationId
                : !string.IsNullOrWhiteSpace(runId)
                    ? runId
                    : executionId;

            return new AiRuntimeLedgerEventCorrelationContext
            {
                ExecutionId = executionId,
                RunId = runId,
                PipelineName = pipelineKey,

                // Logical DAG step identity.
                StepId = stepName,

                // Technical implementation/type key.
                StepKey = resolvedStepKey,

                RuntimeInstanceId = resolvedRuntimeInstanceId,
                WorkerId = workerId,
                ClaimToken = claimToken,
                Provider = concurrencyContext?.Provider,
                Model = concurrencyContext?.Model,
                Operation = concurrencyContext?.Operation,
                CorrelationId = resolvedCorrelationId
            };
        }
    }
}