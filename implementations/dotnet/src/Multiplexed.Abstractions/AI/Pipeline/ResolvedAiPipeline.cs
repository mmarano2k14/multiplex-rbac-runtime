using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;

namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Represents a fully resolved executable AI pipeline.
    ///
    /// This model is the runtime-ready version of a declarative pipeline definition.
    /// It contains:
    /// - pipeline identity
    /// - execution mode
    /// - the resolved ordered step instances
    ///
    /// IMPORTANT:
    /// ExecutionMode determines how the pipeline must be executed:
    /// - Sequential: index-based step progression
    /// - Dag: dependency-driven step scheduling
    /// </summary>
    public sealed class ResolvedAiPipeline
    {
        /// <summary>
        /// Gets or sets the pipeline name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional pipeline version.
        /// </summary>
        public string? Version { get; init; }

        /// <summary>
        /// Gets or sets the execution mode for this pipeline.
        ///
        /// This value determines which execution engine should be used
        /// when running the pipeline.
        /// </summary>
        public AiExecutionMode ExecutionMode { get; init; } = AiExecutionMode.Sequential;

        /// <summary>
        /// Gets or sets the resolved executable steps of the pipeline.
        /// </summary>
        public IReadOnlyList<ResolvedAiPipelineStep> Steps { get; init; }
            = Array.Empty<ResolvedAiPipelineStep>();

        /// <summary>
        /// Gets or initializes the resolved pipeline-level configuration.
        /// </summary>
        /// <remarks>
        /// This configuration acts as the default configuration inherited by resolved steps.
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