namespace Multiplexed.AI.Configuration
{
    /// <summary>
    /// Defines configuration options for the AI runtime engine.
    ///
    /// Responsibilities:
    /// - Select the default pipeline definition source
    /// - Provide source-specific settings
    /// - Prepare the runtime for future extensibility
    ///
    /// Notes:
    /// - The execution engine does not consume these options directly.
    /// - These options are primarily used by pipeline preparation components,
    ///   such as source selection and provider wiring.
    /// </summary>
    public sealed class AiEngineOptions
    {
        /// <summary>
        /// Gets or sets the default pipeline definition source.
        ///
        /// Expected values:
        /// - InMemory
        /// - Json
        ///
        /// Additional values may be introduced later.
        /// </summary>
        public string DefaultPipelineDefinitionSource { get; set; } = "Json";

        /// <summary>
        /// Gets or sets the optional JSON file path used by the JSON pipeline definition provider.
        /// </summary>
        public string? JsonPipelineDefinitionFilePath { get; set; }

        /// <summary>
        /// Gets or sets the optional default pipeline name used when no explicit pipeline name is provided.
        /// </summary>
        public string? DefaultPipelineName { get; set; }
    }
}