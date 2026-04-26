using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Execution.State
{
    /// <summary>
    /// Provides payload-aware read access to an AI execution state.
    ///
    /// PURPOSE:
    /// - Keeps <see cref="AiExecutionState"/> focused on durable state and mutation.
    /// - Centralizes read behavior for inline and payload-backed values.
    /// - Hides payload resolution from callers.
    ///
    /// RULE:
    /// - Payload-backed values take precedence over inline values.
    /// - Inline values remain the fallback for backward compatibility.
    /// </summary>
    public interface IAiExecutionStateReader
    {
        /// <summary>
        /// Reads an execution data value from payload storage when available,
        /// otherwise from the inline execution data bag.
        /// </summary>
        Task<T?> GetDataAsync<T>(
            AiExecutionState state,
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads an execution metadata value from payload storage when available,
        /// otherwise from the inline metadata bag.
        /// </summary>
        Task<T?> GetMetadataAsync<T>(
            AiExecutionState state,
            string key,
            CancellationToken cancellationToken = default);
    }
}