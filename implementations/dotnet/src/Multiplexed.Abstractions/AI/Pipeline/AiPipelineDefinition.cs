using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
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

        /// <summary>
        /// Gets or initializes pipeline-level configuration shared by all steps.
        /// </summary>
        /// <remarks>
        /// Step-level configuration may override these values during policy/config resolution.
        /// </remarks>
        public IReadOnlyDictionary<string, object?> Config { get; init; }
            = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets the optional parallel execution definition.
        ///
        /// This definition controls bounded parallel execution behavior
        /// for DAG-based execution engines.
        /// </summary>
        /// <remarks>
        /// PURPOSE:
        /// - Defines how many DAG steps may execute concurrently.
        /// - Enables config-driven parallel orchestration.
        /// - Allows different pipelines to expose different execution concurrency models.
        ///
        /// IMPORTANT:
        /// - This definition is orchestration-level metadata.
        /// - It is intentionally strongly typed and not stored in the generic Config bag.
        /// - Sequential execution engines may safely ignore this definition.
        /// - DAG execution engines may use this definition to configure bounded parallelism.
        ///
        /// FUTURE EXTENSIBILITY:
        /// This definition may later evolve to support:
        /// - distributed concurrency limits
        /// - admission control
        /// - worker affinity
        /// - scheduling strategies
        /// - partition-aware execution
        /// </remarks>
        public AiParallelExecutionDefinition? ParallelExecution { get; init; }
    }
}