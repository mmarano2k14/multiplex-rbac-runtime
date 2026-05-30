namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Represents a DAG graph snapshot of an AI execution.
    /// </summary>
    public sealed class AiExecutionGraph
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the graph nodes (steps).
        /// </summary>
        public List<AiExecutionGraphNode> Nodes { get; set; } = new();

        /// <summary>
        /// Gets or sets the graph edges (dependencies).
        /// </summary>
        public List<AiExecutionGraphEdge> Edges { get; set; } = new();
    }

    /// <summary>
    /// Represents a node in the execution graph.
    /// </summary>
    public sealed class AiExecutionGraphNode
    {
        /// <summary>
        /// Gets or sets the step identifier.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the step status.
        /// </summary>
        public string Status { get; set; } = default!;

        /// <summary>
        /// Gets or sets the retry count.
        /// </summary>
        public int RetryCount { get; set; }
    }

    /// <summary>
    /// Represents a dependency edge between steps.
    /// </summary>
    public sealed class AiExecutionGraphEdge
    {
        /// <summary>
        /// Gets or sets the source step.
        /// </summary>
        public string From { get; set; } = default!;

        /// <summary>
        /// Gets or sets the target step.
        /// </summary>
        public string To { get; set; } = default!;
    }
}