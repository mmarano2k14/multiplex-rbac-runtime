using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    internal static class AiExecutionCleanupDecider
    {
        public static bool ShouldCleanup(
            AiExecutionRecord record,
            AiExecutionCleanupOptions options)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(options);

            return record.Status switch
            {
                AiExecutionStatus.Completed => options.AutoCleanupOnCompleted,
                AiExecutionStatus.Failed => options.AutoCleanupOnFailed,
                _ => false
            };
        }

        public static async Task TryCleanupAsync(
            AiExecutionRecord record,
            IAiExecutionCleanupService cleanupService,
            IAiRuntimeLogger logger,
            AiExecutionCleanupOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(cleanupService);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(options);

            if (!ShouldCleanup(record, options))
            {
                logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' with status '{record.Status}'.");

                return;
            }

            logger.Engine.LogInformation(
                $"[AI CLEANUP] Starting for execution '{record.ExecutionId}' with status '{record.Status}'.");

            try
            {
                await cleanupService.DeleteExecutionBundleAsync(
                    record,
                    cancellationToken);

                logger.Engine.LogInformation(
                    $"[AI CLEANUP] Completed for execution '{record.ExecutionId}'.");
            }
            catch (Exception ex)
            {
                logger.Engine.LogError(
                    ex,
                    $"[AI CLEANUP] Failed for execution '{record.ExecutionId}'.");

                if (!options.SuppressCleanupExceptions)
                {
                    throw;
                }
            }
        }
    }
}