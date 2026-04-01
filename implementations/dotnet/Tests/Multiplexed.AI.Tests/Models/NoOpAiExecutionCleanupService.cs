using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    public sealed class NoOpAiExecutionCleanupService : IAiExecutionCleanupService
    {
        public Task CleanupAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<bool> DeleteExecutionBundleAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}