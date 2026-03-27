namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Selects the pipeline definition provider to use for a given pipeline request.
    ///
    /// Architecture summary:
    /// AiExecutionEngine
    ///     -> IAiPipelineExecutor.PrepareAsync(...)
    ///         -> IAiPipelineDefinitionSourceSelector
    ///             -> IAiPipelineDefinitionProvider
    ///
    /// Responsibilities:
    /// - Select the appropriate pipeline definition provider
    /// - Keep provider selection out of the execution engine
    /// - Support future multi-source pipeline resolution
    /// </summary>
    public interface IAiPipelineDefinitionSourceSelector
    {
        /// <summary>
        /// Selects the pipeline definition provider to use for the specified pipeline name.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <returns>The selected pipeline definition provider.</returns>
        IAiPipelineDefinitionProvider Select(string pipelineName);
    }
}