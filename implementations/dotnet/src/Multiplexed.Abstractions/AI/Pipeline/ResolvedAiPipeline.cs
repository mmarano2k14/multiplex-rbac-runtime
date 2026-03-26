namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Represents a resolved executable AI pipeline plan.
    /// This is the runtime-ready form produced by the pipeline resolver
    /// from a provider-neutral declarative pipeline definition.
    /// </summary>
    public sealed class ResolvedAiPipeline
    {
        /// <summary>
        /// Gets or sets the unique pipeline name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional pipeline version.
        /// </summary>
        public string? Version { get; init; }

        /// <summary>
        /// Gets or sets the ordered resolved steps that can be executed by the runtime.
        /// </summary>
        public IReadOnlyCollection<ResolvedAiPipelineStep> Steps { get; init; }
            = Array.Empty<ResolvedAiPipelineStep>();
    }
}