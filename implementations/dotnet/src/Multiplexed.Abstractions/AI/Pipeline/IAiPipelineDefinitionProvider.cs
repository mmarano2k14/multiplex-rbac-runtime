namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Provides declarative AI pipeline definitions from any underlying source.
    /// Implementations may load pipeline definitions from memory, JSON files,
    /// databases, remote APIs, or any other provider-specific source.
    /// </summary>
    public interface IAiPipelineDefinitionProvider
    {
        /// <summary>
        /// Gets the pipeline definition for the specified pipeline name.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <param name="cancellationToken">The cancellation token for the active operation.</param>
        /// <returns>The matching pipeline definition.</returns>
        Task<AiPipelineDefinition> GetDefinitionAsync(
            string pipelineName,
            CancellationToken cancellationToken = default);
    }
}