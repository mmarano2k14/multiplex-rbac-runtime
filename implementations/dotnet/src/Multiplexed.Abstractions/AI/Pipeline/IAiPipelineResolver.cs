namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Resolves a declarative AI pipeline definition into an executable
    /// runtime pipeline plan.
    /// </summary>
    public interface IAiPipelineResolver
    {
        /// <summary>
        /// Resolves the specified pipeline definition into an executable plan.
        /// </summary>
        /// <param name="definition">The pipeline definition to resolve.</param>
        /// <param name="cancellationToken">The cancellation token for the active operation.</param>
        /// <returns>The resolved runtime pipeline plan.</returns>
        Task<ResolvedAiPipeline> ResolveAsync(
            AiPipelineDefinition definition,
            CancellationToken cancellationToken = default);
    }
}