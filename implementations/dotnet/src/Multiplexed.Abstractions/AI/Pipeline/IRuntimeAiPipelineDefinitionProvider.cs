namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Provides a mutable runtime pipeline definition catalog.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider is used for dynamically published pipeline definitions submitted
    /// through runtime pipeline run requests.
    /// </para>
    /// <para>
    /// It is separate from <see cref="InMemoryAiPipelineDefinitionProvider"/>, which is
    /// initialized from definitions known at startup and is intentionally read-only.
    /// </para>
    /// </remarks>
    public interface IRuntimeAiPipelineDefinitionProvider : IAiPipelineDefinitionProvider
    {
        /// <summary>
        /// Publishes or replaces a pipeline definition in the runtime catalog.
        /// </summary>
        /// <param name="definition">The pipeline definition to publish.</param>
        void Upsert(
            AiPipelineDefinition definition);
    }
}