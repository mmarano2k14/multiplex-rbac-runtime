using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Runtime.Logging;
using System.Reflection;

namespace Multiplexed.AI.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Executes individual AI steps using attribute-driven retry behavior,
    /// minimal idempotency, and execution metadata tracking.
    ///
    /// Responsibilities:
    /// - Read retry configuration from <see cref="AiRetryPolicyAttribute"/>
    /// - Classify retryable exceptions
    /// - Track execution metadata per step
    /// - Prevent re-execution of already completed steps
    /// - Emit structured runtime events for observability
    ///
    /// This class is intentionally focused on the execution of a single step.
    /// It does not own workflow orchestration or persistence orchestration.
    /// </summary>
    public sealed class AiStepExecutor : IAiStepExecutor
    {

        private readonly IAiRetryExceptionClassifier _exceptionClassifier;
        private readonly IAiRuntimeLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepExecutor"/> class.
        /// </summary>
        /// <param name="exceptionClassifier">The retry classifier used to evaluate transient exceptions.</param>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        /// <param name="logger">The centralized AI runtime logger responsible for structured tracing across engine, pipeline, and step execution.</param>
        public AiStepExecutor(
            IAiRetryExceptionClassifier exceptionClassifier,
            IAiRuntimeLogger logger)
        {

            ArgumentNullException.ThrowIfNull(exceptionClassifier);
            ArgumentNullException.ThrowIfNull(logger);

            _exceptionClassifier = exceptionClassifier;
            _logger = logger;
        }

        /// <summary>
        /// Executes the specified step using retry behavior declared by attribute.
        /// </summary>
        /// <param name="step">The step to execute.</param>
        /// <param name="context">The current shared execution context.</param>
        /// <param name="cancellationToken">The cancellation token for the active execution.</param>
        /// <returns>The final step result.</returns>
        public async Task<AiStepResult> ExecuteAsync(
            IAiStep step,
            AiExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(context);

            var stepType = step.GetType();
            var stepName = step.Name;
            var retryPolicy = stepType.GetCustomAttribute<AiRetryPolicyAttribute>(inherit: true);
            var metadata = GetOrCreateStepMetadata(context.State, stepName);

            // Minimal idempotency:
            // if the step has already completed successfully, do not execute it again.
            if (metadata.IsCompleted)
            {

                _logger.StepExecutor.Skipped(context.ExecutionId, stepName);

                return AiStepResult.Ok(
                    output: $"Step '{stepName}' was skipped because it had already completed successfully.");
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    BeginAttempt(metadata);

                    _logger.StepExecutor.AttemptStarted(context.ExecutionId, stepName, metadata.AttemptCount);

                    var result = await step.ExecuteAsync(context, cancellationToken);

                    if (!result.Success)
                    {
                        metadata.Status = AiStepExecutionStatus.Failed;
                        metadata.LastError = result.Error;
                        metadata.LastExceptionType = null;

                        _logger.StepExecutor.AttemptFailed(context.ExecutionId, stepName, metadata.AttemptCount, result.Error);

                        return result;
                    }

                    MarkSucceeded(metadata);

                    _logger.StepExecutor.AttemptSucceeded(context.ExecutionId, stepName, metadata.AttemptCount);

                    return result;
                }
                catch (Exception ex)
                {
                    metadata.Status = AiStepExecutionStatus.Failed;
                    metadata.LastError = ex.Message;
                    metadata.LastExceptionType = ex.GetType().FullName;

                    _logger.StepExecutor.AttemptException(context.ExecutionId, stepName, metadata.AttemptCount, ex);

                    if (!ShouldRetry(ex, retryPolicy, metadata.AttemptCount))
                    {
                        throw;
                    }

                    var delay = ComputeDelay(retryPolicy!, metadata.AttemptCount);
                    if (delay > TimeSpan.Zero)
                    {
                        _logger.StepExecutor.RetryScheduled(context.ExecutionId, stepName, metadata.AttemptCount, delay);

                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }
        }

        /// <summary>
        /// Begins a new execution attempt for the specified step metadata.
        /// </summary>
        private static void BeginAttempt(AiStepExecutionMetadata metadata)
        {
            metadata.AttemptCount++;
            metadata.Status = AiStepExecutionStatus.Running;
            metadata.LastStartedAtUtc = DateTimeOffset.UtcNow;
            metadata.FirstStartedAtUtc ??= metadata.LastStartedAtUtc;
        }

        /// <summary>
        /// Marks the step metadata as successfully completed.
        /// </summary>
        private static void MarkSucceeded(AiStepExecutionMetadata metadata)
        {
            metadata.Status = AiStepExecutionStatus.Succeeded;
            metadata.CompletedAtUtc = DateTimeOffset.UtcNow;
            metadata.LastError = null;
            metadata.LastExceptionType = null;
        }

        /// <summary>
        /// Determines whether a failed attempt should be retried.
        /// </summary>
        private bool ShouldRetry(
            Exception exception,
            AiRetryPolicyAttribute? retryPolicy,
            int attemptCount)
        {
            if (retryPolicy is null)
                return false;

            if (attemptCount > retryPolicy.MaxRetries)
                return false;

            if (!retryPolicy.RetryTransientOnly)
                return true;

            return _exceptionClassifier.IsRetryable(exception);
        }

        /// <summary>
        /// Computes the retry delay for the next attempt.
        /// </summary>
        private static TimeSpan ComputeDelay(
            AiRetryPolicyAttribute retryPolicy,
            int attemptCount)
        {
            if (retryPolicy.DelayMilliseconds <= 0)
                return TimeSpan.Zero;

            return retryPolicy.BackoffMode switch
            {
                AiRetryBackoffMode.Fixed
                    => TimeSpan.FromMilliseconds(retryPolicy.DelayMilliseconds),

                AiRetryBackoffMode.Exponential
                    => TimeSpan.FromMilliseconds(
                        retryPolicy.DelayMilliseconds * Math.Pow(2, Math.Max(0, attemptCount - 1))),

                _ => TimeSpan.FromMilliseconds(retryPolicy.DelayMilliseconds)
            };
        }

        /// <summary>
        /// Returns existing metadata for the specified step or creates a new one.
        /// </summary>
        private static AiStepExecutionMetadata GetOrCreateStepMetadata(
            AiExecutionState state,
            string stepName)
        {
            if (!state.Metadata.TryGetValue(AiExecutionKeys.StepExecutionMetadata, out var existing) ||
                existing is not Dictionary<string, AiStepExecutionMetadata> stepMap)
            {
                stepMap = new Dictionary<string, AiStepExecutionMetadata>(StringComparer.Ordinal);
                state.Metadata[AiExecutionKeys.StepExecutionMetadata] = stepMap;
            }

            if (!stepMap.TryGetValue(stepName, out var metadata))
            {
                metadata = new AiStepExecutionMetadata
                {
                    StepName = stepName
                };

                stepMap[stepName] = metadata;
            }

            state.UpdatedAtUtc = DateTime.UtcNow;

            return metadata;
        }
    }
}