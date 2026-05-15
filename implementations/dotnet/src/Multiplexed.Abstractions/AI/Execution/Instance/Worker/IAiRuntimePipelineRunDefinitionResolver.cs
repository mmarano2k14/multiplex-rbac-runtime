using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Resolves the pipeline definition associated with a runtime pipeline run request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pipeline run request may provide its definition through raw JSON, a JSON file
    /// path, or an in-memory <see cref="AiPipelineDefinition"/> instance.
    /// </para>
    /// <para>
    /// The resolver is responsible for selecting the effective source, validating that
    /// the requested pipeline name exists, and returning the matching pipeline definition.
    /// </para>
    /// <para>
    /// Source priority is:
    /// raw JSON first, JSON file path second, in-memory pipeline definition third.
    /// </para>
    /// </remarks>
    public interface IAiRuntimePipelineRunDefinitionResolver
    {
        /// <summary>
        /// Resolves the pipeline definition for the specified run request.
        /// </summary>
        /// <param name="request">
        /// The pipeline run request.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The resolved pipeline definition matching the request pipeline name.
        /// </returns>
        Task<AiPipelineDefinition> ResolveAsync(
            AiRuntimePipelineRunRequest request,
            CancellationToken cancellationToken = default);
    }
}