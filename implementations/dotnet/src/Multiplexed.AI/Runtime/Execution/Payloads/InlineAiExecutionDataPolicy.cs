using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Stores all execution payloads inline.
    ///
    /// PURPOSE:
    /// - Provides the first no-behavior-change implementation of the execution
    ///   payload policy.
    /// - Allows the runtime to introduce payload indirection without externalizing
    ///   any data yet.
    ///
    /// IMPORTANT:
    /// - This implementation is intentionally conservative.
    /// - It is the compatibility bridge before artifact-backed storage is enabled.
    /// </summary>
    public sealed class InlineAiExecutionDataPolicy : IAiExecutionDataPolicy
    {
        public Task<AiStoredPayload> StoreAsync(
            object? value,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AiStoredPayload.Inline(value));
        }
    }
}