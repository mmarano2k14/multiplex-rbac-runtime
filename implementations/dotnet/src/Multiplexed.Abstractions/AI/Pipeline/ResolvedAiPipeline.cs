using Multiplexed.Abstractions.AI.Execution;

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
    }
}