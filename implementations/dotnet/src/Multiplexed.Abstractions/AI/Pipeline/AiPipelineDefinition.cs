using Multiplexed.Abstractions.AI.Execution;
using System.Text.Json.Serialization;

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
        /// Gets or sets the execution mode of the pipeline.
        ///
        /// This determines how the pipeline must be orchestrated:
        /// - Sequential: index-based progression
        /// - Dag: dependency-driven scheduling
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AiExecutionMode ExecutionMode { get; init; } = AiExecutionMode.Sequential;

        /// <summary>
        /// Gets or sets the ordered declarative step definitions.
        ///
        /// In sequential mode, order defines execution progression.
        /// In DAG mode, order remains useful as a deterministic tie-breaker,
        /// but dependency resolution is driven by step-level DependsOn declarations.
        /// </summary>
        public IReadOnlyCollection<AiPipelineStepDefinition> Steps { get; init; }
            = Array.Empty<AiPipelineStepDefinition>();
    }
}