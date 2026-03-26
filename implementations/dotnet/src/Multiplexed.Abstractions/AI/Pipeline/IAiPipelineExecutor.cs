using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Defines the contract for pipeline-specific execution over a persisted AI execution.
    ///
    /// Architecture summary:
    /// AiExecutionEngine
    ///     -> IAiPipelineExecutor
    ///         -> IAiPipelineDefinitionProvider
    ///         -> IAiPipelineResolver
    ///         -> IAiStepExecutor
    ///
    /// Responsibilities:
    /// - Resolve a provider-neutral pipeline definition into a runtime pipeline
    /// - Select the current step from the orchestration record
    /// - Execute exactly one resolved step
    /// - Return structured pipeline progression data to the engine
    ///
    /// The pipeline executor does not own:
    /// - RBAC context loading
    /// - RBAC context rotation
    /// - optimistic concurrency
    /// - persistence orchestration
    /// </summary>
    public interface IAiPipelineExecutor
    {
        /// <summary>
        /// Resolves the specified pipeline into an executable runtime pipeline.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The resolved executable pipeline.</returns>
        Task<ResolvedAiPipeline> PrepareAsync(
            string pipelineName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the current pipeline step for the supplied execution context
        /// using the provided resolved pipeline.
        /// </summary>
        /// <param name="pipeline">The resolved executable pipeline.</param>
        /// <param name="context">The shared execution context for the current step.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The pipeline execution result for the executed step.</returns>
        Task<PipelineExecutionResult> ExecuteNextAsync(
            ResolvedAiPipeline pipeline,
            AiExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}