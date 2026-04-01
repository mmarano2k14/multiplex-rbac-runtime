using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    /// <summary>
    /// Defines coordinated cleanup of an AI execution bundle.
    /// </summary>
    public interface IAiExecutionCleanupService
    {
        Task CleanupAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteExecutionBundleAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default);
    }
}