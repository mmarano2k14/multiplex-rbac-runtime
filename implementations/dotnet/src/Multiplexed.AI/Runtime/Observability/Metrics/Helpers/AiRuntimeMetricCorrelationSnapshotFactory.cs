using Multiplexed.Abstractions.AI.Observability.Context;

namespace Multiplexed.AI.Runtime.Observability.Metrics.Helpers
{
    /// <summary>
    /// Creates detached metric correlation snapshots from ambient runtime correlation contexts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Metric records must store a detached snapshot instead of the mutable ambient
    /// correlation context instance. This prevents later enrichment of the ambient context
    /// from changing already-created metric records.
    /// </para>
    /// </remarks>
    internal static class AiRuntimeMetricCorrelationSnapshotFactory
    {
        /// <summary>
        /// Creates a detached runtime execution correlation context snapshot.
        /// </summary>
        /// <param name="context">The ambient runtime execution correlation context.</param>
        /// <returns>A detached runtime execution correlation context snapshot.</returns>
        public static AiRuntimeExecutionCorrelationContext Create(
            AiRuntimeExecutionCorrelationContext? context)
        {
            return new AiRuntimeExecutionCorrelationContext
            {
                CorrelationId = context?.CorrelationId ?? string.Empty,
                RunId = context?.RunId,
                ExecutionId = context?.ExecutionId,
                PipelineName = context?.PipelineName,
                PipelineVersion = context?.PipelineVersion,
                PipelineKey = context?.PipelineKey,
                RuntimeInstanceId = context?.RuntimeInstanceId,
                WorkerId = context?.WorkerId ?? context?.RuntimeInstanceId
            };
        }
    }
}