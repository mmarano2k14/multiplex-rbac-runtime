using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Tests.Models
{
    /// <summary>
    /// In-memory pipeline definition provider used for tests.
    /// </summary>
    public sealed class FakeInMemoryAiPipelineDefinitionProvider : IAiPipelineDefinitionProvider
    {
        private readonly IReadOnlyDictionary<string, AiPipelineDefinition> _definitions;

        public FakeInMemoryAiPipelineDefinitionProvider(IEnumerable<AiPipelineDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            _definitions = definitions.ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);
        }

        public Task<AiPipelineDefinition> GetDefinitionAsync(
            string pipelineName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            if (!_definitions.TryGetValue(pipelineName, out var definition))
            {
                throw new InvalidOperationException(
                    $"Pipeline definition '{pipelineName}' was not found.");
            }

            return Task.FromResult(definition);
        }
    }
}