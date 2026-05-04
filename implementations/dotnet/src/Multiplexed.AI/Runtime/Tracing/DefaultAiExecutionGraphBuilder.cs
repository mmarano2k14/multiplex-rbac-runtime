using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Tracing;
using System;
using System.Linq;

namespace Multiplexed.AI.Runtime.Tracing
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionGraphBuilder"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Builds a UI/export-friendly DAG graph from the current execution state.
    /// - Converts step state into graph nodes and dependency metadata into graph edges.
    ///
    /// DESIGN:
    /// - The execution state remains the source of truth.
    /// - The graph is a read-only projection for diagnostics and visualization.
    /// - This builder does not mutate execution state.
    ///
    /// IMPORTANT:
    /// - Retained hot-state steps are included directly.
    /// - Evicted/archived steps are only included if they are present in the provided state.
    /// - If archived steps must appear after retention, the caller should provide a rehydrated or resolver-enriched state.
    /// </remarks>
    public sealed class DefaultAiExecutionGraphBuilder : IAiExecutionGraphBuilder
    {
        /// <inheritdoc />
        public AiExecutionGraph Build(
            string executionId,
            AiExecutionState state)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(state);

            var graph = new AiExecutionGraph
            {
                ExecutionId = executionId
            };

            foreach (var step in state.Steps.Values.OrderBy(x => x.StepName, StringComparer.Ordinal))
            {
                graph.Nodes.Add(new AiExecutionGraphNode
                {
                    Id = step.StepName,
                    Status = step.Status.ToString(),
                    RetryCount = step.RetryState?.RetryCount ?? 0
                });

                foreach (var dependency in step.DependsOn.OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(dependency))
                    {
                        continue;
                    }

                    graph.Edges.Add(new AiExecutionGraphEdge
                    {
                        From = dependency,
                        To = step.StepName
                    });
                }
            }

            return graph;
        }
    }
}