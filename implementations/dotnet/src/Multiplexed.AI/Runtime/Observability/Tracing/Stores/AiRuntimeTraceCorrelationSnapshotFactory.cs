using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Observability.Tracing;

namespace Multiplexed.AI.Runtime.Observability.Tracing.Stores
{
    /// <summary>
    /// Creates detached tracing correlation snapshots from the current runtime
    /// execution correlation context and trace-local dimensions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PURPOSE:
    /// - Keeps trace records independent from the mutable ambient correlation context.
    /// - Prevents later AsyncLocal changes from modifying already-created trace records.
    /// - Aligns tracing correlation with ledger and metrics correlation.
    /// </para>
    ///
    /// <para>
    /// DESIGN:
    /// - Runtime run/execution/worker correlation is copied into
    ///   <see cref="AiRuntimeTraceCorrelationContext.Runtime"/>.
    /// - Trace-specific dimensions such as step, claim token, provider, model,
    ///   operation, payload references, and scope identifiers are stored directly on
    ///   <see cref="AiRuntimeTraceCorrelationContext"/>.
    /// </para>
    ///
    /// <para>
    /// IMPORTANT:
    /// - This factory does not read durable state.
    /// - This factory does not write metrics, traces, or ledger entries.
    /// - It only creates immutable-style snapshots for observability.
    /// </para>
    /// </remarks>
    internal static class AiRuntimeTraceCorrelationSnapshotFactory
    {
        /// <summary>
        /// Creates a trace correlation snapshot.
        /// </summary>
        /// <param name="current">The current ambient runtime correlation context.</param>
        /// <param name="executionId">The explicit execution identifier, when known.</param>
        /// <param name="stepId">The explicit logical step identifier, when known.</param>
        /// <param name="stepKey">The explicit technical step key, when known.</param>
        /// <param name="workerId">The explicit worker identifier, when known.</param>
        /// <param name="claimToken">The explicit claim token, when known.</param>
        /// <param name="provider">The provider associated with the operation.</param>
        /// <param name="model">The model associated with the operation.</param>
        /// <param name="operation">The logical operation associated with the trace.</param>
        /// <param name="traceId">The distributed trace identifier.</param>
        /// <param name="traceScopeId">The trace scope identifier.</param>
        /// <param name="parentTraceScopeId">The parent trace scope identifier.</param>
        /// <param name="source">The trace source.</param>
        /// <returns>The detached trace correlation snapshot.</returns>
        public static AiRuntimeTraceCorrelationContext Create(
            AiRuntimeExecutionCorrelationContext? current,
            string? executionId = null,
            string? stepId = null,
            string? stepKey = null,
            string? workerId = null,
            string? claimToken = null,
            string? provider = null,
            string? model = null,
            string? operation = null,
            string? traceId = null,
            string? traceScopeId = null,
            string? parentTraceScopeId = null,
            string? source = null)
        {
            var resolvedExecutionId = FirstNonEmpty(
                executionId,
                current?.ExecutionId);

            var resolvedWorkerId = FirstNonEmpty(
                workerId,
                current?.WorkerId,
                current?.RuntimeInstanceId);

            return new AiRuntimeTraceCorrelationContext
            {
                Runtime = current is null
                    ? null
                    : new AiRuntimeExecutionCorrelationContext
                    {
                        CorrelationId = current.CorrelationId,
                        RunId = current.RunId,
                        ExecutionId = resolvedExecutionId,
                        PipelineName = current.PipelineName,
                        PipelineVersion = current.PipelineVersion,
                        PipelineKey = current.PipelineKey,
                        RuntimeInstanceId = current.RuntimeInstanceId,
                        WorkerId = resolvedWorkerId
                    },

                StepId = Normalize(stepId),
                StepKey = Normalize(stepKey),
                ClaimToken = Normalize(claimToken),
                Provider = Normalize(provider),
                Model = Normalize(model),
                Operation = Normalize(operation),
                TraceId = Normalize(traceId),
                TraceScopeId = Normalize(traceScopeId),
                ParentTraceScopeId = Normalize(parentTraceScopeId),
                Source = Normalize(source) ?? "runtime-tracing"
            };
        }

        /// <summary>
        /// Returns the first non-empty value from the supplied values.
        /// </summary>
        /// <param name="values">The candidate values.</param>
        /// <returns>The first non-empty value, or <c>null</c>.</returns>
        private static string? FirstNonEmpty(
            params string?[] values)
        {
            foreach (var value in values)
            {
                var normalized = Normalize(value);

                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        /// <summary>
        /// Normalizes a correlation value.
        /// </summary>
        /// <param name="value">The value to normalize.</param>
        /// <returns>The normalized value, or <c>null</c>.</returns>
        private static string? Normalize(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}