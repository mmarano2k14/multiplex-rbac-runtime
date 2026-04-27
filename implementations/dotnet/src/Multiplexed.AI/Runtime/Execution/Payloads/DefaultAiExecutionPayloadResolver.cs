using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Default execution payload resolver.
    ///
    /// PURPOSE:
    /// - Resolves inline and artifact-backed payloads.
    /// - Keeps consumers unaware of the physical payload store.
    ///
    /// DESIGN:
    /// - Inline payloads are returned directly.
    /// - Artifact-backed payloads are loaded through the configured store resolver.
    ///
    /// IMPORTANT:
    /// - Missing artifacts are invalid replay/recovery state.
    /// - Resolver does not decide storage provider; it uses <see cref="IAiPayloadStoreResolver"/>.
    /// </summary>
    public sealed class DefaultAiExecutionPayloadResolver : IAiExecutionPayloadResolver
    {
        private readonly IAiPayloadStoreResolver _storeResolver;

        public DefaultAiExecutionPayloadResolver(
            IAiPayloadStoreResolver storeResolver)
        {
            ArgumentNullException.ThrowIfNull(storeResolver);

            _storeResolver = storeResolver;
        }

        public async Task<object?> ResolveAsync(
            AiStoredPayload payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(payload);

            if (payload.IsInline)
            {
                return payload.InlineValue;
            }

            if (string.IsNullOrWhiteSpace(payload.ArtifactId))
            {
                throw new InvalidOperationException(
                    "Artifact-backed execution payload does not contain an artifact id.");
            }

            var store = _storeResolver.Resolve();

            var content = await store.LoadAsync(
                payload.ArtifactId,
                cancellationToken);

            return content is null
                ? throw new InvalidOperationException(
                    $"Execution payload artifact '{payload.ArtifactId}' could not be resolved.")
                : JsonSerializer.Deserialize<object>(content);
        }
    }
}