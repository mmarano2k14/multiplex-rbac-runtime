using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline.Definition
{
    /// <summary>
    /// Thread-safe runtime pipeline definition provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider stores pipeline definitions that are published dynamically at
    /// runtime, for example by the pipeline background controller before creating
    /// an execution.
    /// </para>
    /// <para>
    /// Each published definition is stored by name. Publishing a definition with the
    /// same name replaces the previous runtime definition.
    /// </para>
    /// </remarks>
    public sealed class RuntimeAiPipelineDefinitionProvider : IRuntimeAiPipelineDefinitionProvider
    {
        private readonly ConcurrentDictionary<string, AiPipelineDefinition> _definitions =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task<AiPipelineDefinition> GetDefinitionAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (_definitions.TryGetValue(name, out var definition))
            {
                return Task.FromResult(definition);
            }

            throw new KeyNotFoundException(
                $"Runtime pipeline definition '{name}' was not found.");
        }

        /// <inheritdoc />
        public void Upsert(
            AiPipelineDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);

            _definitions[definition.Name] = definition;
        }
    }
}