namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Represents the declarative definition of an AI pipeline.
    /// This model is intentionally provider-agnostic so that pipeline
    /// definitions can later be loaded from in-memory registration,
    /// JSON files, databases, or any other external source.
    /// </summary>
    public sealed class AiPipelineDefinition
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
        /// Gets or sets the ordered declarative step definitions.
        /// </summary>
        public IReadOnlyCollection<AiPipelineStepDefinition> Steps { get; init; }
            = Array.Empty<AiPipelineStepDefinition>();
        /// <summary>
        /// Gets or sets the first step name.
        /// </summary>
        public string FirstStepName { get; init; } = string.Empty;
    }
}