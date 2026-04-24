using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution.Payloads;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Default execution payload resolver.
    ///
    /// PURPOSE:
    /// - Resolves execution payloads without exposing the storage representation to
    ///   runtime components.
    /// - Supports both inline payloads and artifact-backed payloads.
    ///
    /// DESIGN:
    /// - Inline payloads are returned directly.
    /// - Artifact-backed payloads are loaded from <see cref="IAiPayloadStore"/>.
    /// - Artifact content is expected to be serialized JSON.
    ///
    /// IMPORTANT:
    /// - This resolver does not decide where payloads are stored.
    /// - Storage decisions remain owned by <see cref="IAiExecutionDataPolicy"/>.
    /// - This resolver only materializes an already stored payload.
    /// </summary>
    public sealed class DefaultAiExecutionPayloadResolver : IAiExecutionPayloadResolver
    {
        private readonly IAiPayloadStore _payloadStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionPayloadResolver"/> class.
        /// </summary>
        /// <param name="payloadStore">
        /// Store used to resolve artifact-backed execution payloads.
        /// </param>
        public DefaultAiExecutionPayloadResolver(IAiPayloadStore payloadStore)
        {
            ArgumentNullException.ThrowIfNull(payloadStore);

            _payloadStore = payloadStore;
        }

        /// <summary>
        /// Resolves the stored payload into a materialized runtime value.
        ///
        /// BEHAVIOR:
        /// - Inline payloads return <see cref="AiStoredPayload.InlineValue"/> directly.
        /// - Artifact-backed payloads are loaded from <see cref="IAiPayloadStore"/>.
        ///
        /// IMPORTANT:
        /// - Missing artifact identifiers are treated as invalid state.
        /// - Missing artifact content is treated as invalid state because replay or
        ///   binding may depend on the payload being resolvable.
        /// </summary>
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

            var content = await _payloadStore.LoadAsync(
                payload.ArtifactId,
                cancellationToken);

            if (content is null)
            {
                throw new InvalidOperationException(
                    $"Execution payload artifact '{payload.ArtifactId}' could not be resolved.");
            }

            return JsonSerializer.Deserialize<object>(content);
        }
    }
}