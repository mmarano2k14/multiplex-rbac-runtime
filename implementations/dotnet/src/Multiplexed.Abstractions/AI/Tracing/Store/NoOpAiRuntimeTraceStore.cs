using Multiplexed.Abstractions.AI.Tracing;

namespace Multiplexed.Abstractions.AI.Tracing.Store
{
    /// <summary>
    /// No-operation implementation of <see cref="IAiRuntimeTraceStore"/>.
    /// </summary>
    /// <remarks>
    /// This store intentionally ignores trace records. It is used when trace
    /// persistence is disabled.
    /// </remarks>
    public sealed class NoOpAiRuntimeTraceStore : IAiRuntimeTraceStore
    {
        /// <inheritdoc />
        public Task AppendAsync(
            AiTraceRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiTraceRecord>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyList<AiTraceRecord>>(
                Array.Empty<AiTraceRecord>());
        }
    }
}