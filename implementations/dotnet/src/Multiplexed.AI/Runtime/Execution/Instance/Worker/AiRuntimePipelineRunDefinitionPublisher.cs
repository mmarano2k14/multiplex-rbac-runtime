using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimePipelineRunDefinitionPublisher"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This publisher stores dynamically resolved pipeline definitions in the runtime
    /// pipeline definition provider before an execution is created.
    /// </para>
    /// <para>
    /// It is used by the background controller when a pipeline run request supplies
    /// a definition through raw JSON, a JSON file path, or an in-memory definition.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimePipelineRunDefinitionPublisher : IAiRuntimePipelineRunDefinitionPublisher
    {
        private readonly IRuntimeAiPipelineDefinitionProvider _runtimeDefinitionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimePipelineRunDefinitionPublisher"/> class.
        /// </summary>
        /// <param name="runtimeDefinitionProvider">
        /// The mutable runtime pipeline definition provider.
        /// </param>
        public AiRuntimePipelineRunDefinitionPublisher(
            IRuntimeAiPipelineDefinitionProvider runtimeDefinitionProvider)
        {
            _runtimeDefinitionProvider = runtimeDefinitionProvider
                ?? throw new ArgumentNullException(nameof(runtimeDefinitionProvider));
        }

        /// <inheritdoc />
        public Task PublishAsync(
            AiPipelineDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);

            cancellationToken.ThrowIfCancellationRequested();

            _runtimeDefinitionProvider.Upsert(definition);

            return Task.CompletedTask;
        }
    }
}