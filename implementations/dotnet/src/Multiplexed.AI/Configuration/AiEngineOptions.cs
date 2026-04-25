using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.AI.Runtime.Configuration;

namespace Multiplexed.AI.Configuration
{
    /// <summary>
    /// Defines configuration options for the AI runtime engine.
    ///
    /// Responsibilities:
    /// - Select the default pipeline definition source
    /// - Provide source-specific settings
    /// - Group runtime-related option sections
    /// - Prepare the runtime for future extensibility
    ///
    /// Notes:
    /// - The execution engine does not consume every option directly.
    /// - Some sections are primarily used by pipeline preparation components,
    ///   persistence wiring, cleanup behavior, and future runtime services.
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

        /// <summary>
        /// Gets or sets cleanup-related runtime options.
        /// </summary>
        public AiExecutionCleanupOptions Cleanup { get; set; } = new();

        /// <summary>
        /// Gets or sets durable execution snapshot options.
        /// </summary>
        public AiExecutionSnapshotOptions Snapshots { get; set; } = new();

        /// <summary>
        /// Gets or sets payload-store-related runtime options.
        /// </summary>
        public AiPayloadStoreOptions PayloadStore { get; set; } = new();

        
        /// <summary>
        /// Gets or sets execution state retention options.
        /// </summary>
        public AiExecutionStateRetentionOptions StateRetention { get; set; } = new();
    }
}