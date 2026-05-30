using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Builds a graph representation from execution state.
    /// </summary>
    public interface IAiExecutionGraphBuilder
    {
        /// <summary>
        /// Builds a graph from the given execution state.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="state">The execution state.</param>
        /// <returns>The execution graph.</returns>
        AiExecutionGraph Build(string executionId, AiExecutionState state);
    }
}