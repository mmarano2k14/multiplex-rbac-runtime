using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Validation
{
    /// <summary>
    /// Validates enterprise runtime execution results.
    /// </summary>
    public static class EnterpriseRuntimeExecutionValidator
    {
        /// <summary>
        /// Validates an enterprise runtime execution result.
        /// </summary>
        /// <param name="final">
        /// The final execution record returned by the controller.
        /// </param>
        /// <param name="persistedRecord">
        /// The persisted execution record loaded from the DAG store.
        /// </param>
        /// <param name="participatingWorkerCount">
        /// The number of distributed workers that reported activity.
        /// </param>
        /// <param name="retryCount">
        /// The retry count for the configured retried step.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
        public static void Validate(
            AiExecutionRecord final,
            AiExecutionRecord persistedRecord,
            int participatingWorkerCount,
            int retryCount,
            EnterpriseRuntimeExecutionRequest request)
        {
            ArgumentNullException.ThrowIfNull(final);
            ArgumentNullException.ThrowIfNull(persistedRecord);
            ArgumentNullException.ThrowIfNull(request);

            if (!final.IsTerminal)
            {
                throw new InvalidOperationException(
                    "Expected the final execution record to be terminal.");
            }

            if (final.Status != AiExecutionStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Expected final status Completed, but was '{final.Status}'.");
            }

            if (request.ExpectedCompletedStepCount.HasValue &&
                final.CompletedSteps.Count != request.ExpectedCompletedStepCount.Value)
            {
                throw new InvalidOperationException(
                    $"Expected {request.ExpectedCompletedStepCount.Value} completed steps, but found '{final.CompletedSteps.Count}'.");
            }

            if (persistedRecord.Status != AiExecutionStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Expected persisted status Completed, but was '{persistedRecord.Status}'.");
            }

            if (participatingWorkerCount < request.MinimumWorkerCount)
            {
                throw new InvalidOperationException(
                    $"Expected at least '{request.MinimumWorkerCount}' distributed runtime workers to participate, but only '{participatingWorkerCount}' reported activity.");
            }

            if (request.ExpectRetryRecovery && retryCount < 1)
            {
                throw new InvalidOperationException(
                    $"Expected retry recovery for step '{request.RetriedStepName}', but RetryCount was '{retryCount}'.");
            }
        }
    }
}