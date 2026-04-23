namespace Multiplexed.Abstractions.AI.Execution.Payloads
{
    /// <summary>
    /// Decides how execution data should be stored.
    ///
    /// PURPOSE:
    /// - Provides the policy boundary between execution state and artifact storage.
    /// - Allows small values to remain inline while large or unbounded payloads can
    ///   later be externalized.
    ///
    /// DESIGN:
    /// - This policy must not change execution semantics.
    /// - It only decides the storage representation of produced data.
    /// </summary>
    public interface IAiExecutionDataPolicy
    {
        /// <summary>
        /// Creates a stored payload representation for the provided value.
        /// </summary>
        Task<AiStoredPayload> StoreAsync(
            object? value,
            CancellationToken cancellationToken = default);
    }
}