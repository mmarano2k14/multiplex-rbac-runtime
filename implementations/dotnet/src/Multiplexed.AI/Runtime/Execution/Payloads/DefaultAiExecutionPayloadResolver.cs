using Multiplexed.Abstractions.AI.Execution.Payloads;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Default execution payload resolver.
    ///
    /// PURPOSE:
    /// - Resolves execution payloads without exposing the storage representation to
    ///   runtime components.
    /// - Supports inline payloads immediately and provides the extension point for
    ///   artifact-backed payloads later.
    ///
    /// IMPORTANT:
    /// - Artifact resolution is intentionally not implemented in the first
    ///   milestone.
    /// </summary>
    public sealed class DefaultAiExecutionPayloadResolver : IAiExecutionPayloadResolver
    {
        public Task<object?> ResolveAsync(
            AiStoredPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.IsInline)
            {
                return Task.FromResult(payload.InlineValue);
            }

            throw new NotSupportedException(
                "Artifact-backed execution payloads are not supported yet.");
        }
    }
}