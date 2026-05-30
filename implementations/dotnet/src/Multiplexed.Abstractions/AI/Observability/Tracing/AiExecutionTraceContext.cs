namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Trace context for a full AI runtime execution.
    /// </summary>
    public sealed class AiExecutionTraceContext
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string? ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the pipeline identifier, when available.
        /// </summary>
        public string? PipelineId { get; set; }

        /// <summary>
        /// Gets or sets the execution mode, for example Sequential or Dag.
        /// </summary>
        public string? ExecutionMode { get; set; }

        /// <summary>
        /// Gets or sets the current execution status.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets the worker identifier, when the execution is processed by a distributed worker.
        /// </summary>
        public string? WorkerId { get; set; }
    }
}