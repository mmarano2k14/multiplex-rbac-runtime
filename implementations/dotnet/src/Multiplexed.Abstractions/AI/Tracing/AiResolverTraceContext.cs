namespace Multiplexed.Abstractions.AI.Tracing
{
    /// <summary>
    /// Trace context for AI runtime resolver operations.
    /// </summary>
    public sealed class AiResolverTraceContext
    {
        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string? ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the step identifier that requested the resolution, when available.
        /// </summary>
        public string? StepId { get; set; }

        /// <summary>
        /// Gets or sets the resolved path, for example state.cv or steps.step-1.result.data.score.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the resolver source, for example State, Step, Payload, or Context.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets whether the resolver found a value.
        /// </summary>
        public bool Found { get; set; }
    }
}