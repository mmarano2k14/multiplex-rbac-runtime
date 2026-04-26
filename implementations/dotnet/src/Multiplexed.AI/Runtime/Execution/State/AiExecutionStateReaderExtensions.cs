using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;

namespace Multiplexed.AI.Runtime.Execution.State
{
    /// <summary>
    /// Convenience extensions for reading execution state values through
    /// <see cref="IAiExecutionStateReader"/>.
    /// </summary>
    public static class AiExecutionStateReaderExtensions
    {
        /// <summary>
        /// Reads an execution data value using the configured state reader.
        /// </summary>
        public static Task<T?> GetDataAsync<T>(
            this AiExecutionState state,
            IAiExecutionStateReader reader,
            string key,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reader);

            return reader.GetDataAsync<T>(
                state,
                key,
                cancellationToken);
        }

        /// <summary>
        /// Reads an execution metadata value using the configured state reader.
        /// </summary>
        public static Task<T?> GetMetadataAsync<T>(
            this AiExecutionState state,
            IAiExecutionStateReader reader,
            string key,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reader);

            return reader.GetMetadataAsync<T>(
                state,
                key,
                cancellationToken);
        }
    }
}