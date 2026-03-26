using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline.Definition
{
    /// <summary>
    /// Provides AI pipeline definitions from an in-memory collection.
    /// This implementation is intended for development, testing, and
    /// code-first pipeline registration scenarios.
    /// </summary>
    public sealed class InMemoryAiPipelineDefinitionProvider : IAiPipelineDefinitionProvider
    {
        private readonly IReadOnlyDictionary<string, AiPipelineDefinition> _definitions;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryAiPipelineDefinitionProvider"/> class.
        /// </summary>
        /// <param name="definitions">The registered pipeline definitions indexed by name.</param>
        public InMemoryAiPipelineDefinitionProvider(IEnumerable<AiPipelineDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            _definitions = definitions.ToDictionary(
                definition => definition.Name,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the pipeline definition for the specified pipeline name.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <param name="cancellationToken">The cancellation token for the active operation.</param>
        /// <returns>The matching pipeline definition.</returns>
        public Task<AiPipelineDefinition> GetDefinitionAsync(
            string pipelineName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            cancellationToken.ThrowIfCancellationRequested();

            if (!_definitions.TryGetValue(pipelineName, out var definition))
            {
                throw new InvalidOperationException(
                    $"Pipeline definition '{pipelineName}' was not found.");
            }

            return Task.FromResult(definition);
        }
    }
}