using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Publishes a resolved pipeline definition so it can be used by the runtime pipeline resolver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The background controller accepts pipeline definitions from different request sources,
    /// such as raw JSON, JSON files, or in-memory definitions.
    /// </para>
    /// <para>
    /// Before an execution can be created, the resolved pipeline definition must be made
    /// available to the runtime pipeline resolution layer.
    /// </para>
    /// <para>
    /// Implementations may publish definitions to an in-memory provider, a temporary runtime
    /// catalog, or another configured pipeline definition source.
    /// </para>
    /// </remarks>
    public interface IAiRuntimePipelineRunDefinitionPublisher
    {
        /// <summary>
        /// Publishes the specified pipeline definition for runtime execution.
        /// </summary>
        /// <param name="definition">
        /// The pipeline definition to publish.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        Task PublishAsync(
            AiPipelineDefinition definition,
            CancellationToken cancellationToken = default);
    }
}