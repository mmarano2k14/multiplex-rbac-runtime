using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
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
        public async Task<AiStepResult> ExecuteAsync(
    ResolvedAiPipelineStep resolvedStep,
    AiStepExecutionContext context,
    CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resolvedStep);
            ArgumentNullException.ThrowIfNull(resolvedStep.Step);
            ArgumentNullException.ThrowIfNull(context);

            var stepType = resolvedStep.Step.GetType();

            // 🔥 STRONG IDENTITY (support test + runtime)
            var stepName = resolvedStep.Name;
            var fallbackStepName = stepType.Name;

            var retryPolicy = stepType.GetCustomAttribute<AiRetryPolicyAttribute>(inherit: true);

            // 🔥 Try both keys (VERY IMPORTANT)
            var metadata =
                TryGetStepMetadata(context.State, stepName)
                ?? TryGetStepMetadata(context.State, fallbackStepName);

            if (metadata != null && metadata.IsCompleted)
            {
                _logger.StepExecutor.Skipped(context.ExecutionId, stepName);

                return AiStepResult.Ok(
                    output: $"Step '{stepName}' was skipped because it had already completed successfully.");
            }

            // Only create using PRIMARY identity
            metadata ??= GetOrCreateStepMetadata(context.State, stepName);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    BeginAttempt(metadata);

                    _logger.StepExecutor.AttemptStarted(
                        context.ExecutionId,
                        stepName,
                        metadata.AttemptCount);

                    var result = await resolvedStep.Step.ExecuteAsync(
                        context,
                        cancellationToken);

                    if (!result.Success)
                    {
                        metadata.Status = AiStepExecutionStatus.Failed;
                        metadata.LastError = result.Error;
                        metadata.LastExceptionType = null;

                        _logger.StepExecutor.AttemptFailed(
                            context.ExecutionId,
                            stepName,
                            metadata.AttemptCount,
                            result.Error);

                        return result;
                    }

                    MarkSucceeded(metadata);

                    _logger.StepExecutor.AttemptSucceeded(
                        context.ExecutionId,
                        stepName,
                        metadata.AttemptCount);

                    return result;
                }
                catch (Exception ex)
                {
                    metadata.Status = AiStepExecutionStatus.Failed;
                    metadata.LastError = ex.Message;
                    metadata.LastExceptionType = ex.GetType().FullName;

                    _logger.StepExecutor.AttemptException(
                        context.ExecutionId,
                        stepName,
                        metadata.AttemptCount,
                        ex);

                    if (!ShouldRetry(ex, retryPolicy, metadata.AttemptCount))
                    {
                        throw;
                    }

                    var delay = ComputeDelay(retryPolicy!, metadata.AttemptCount);

                    if (delay > TimeSpan.Zero)
                    {
                        _logger.StepExecutor.RetryScheduled(
                            context.ExecutionId,
                            stepName,
                            metadata.AttemptCount,
                            delay);

                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to retrieve existing step metadata without creating it.
        /// </summary>
        private static AiStepExecutionMetadata? TryGetStepMetadata(
            AiExecutionState state,
            string stepName)
        {
            if (!state.Metadata.TryGetValue(AiExecutionKeys.StepExecutionMetadata, out var existing) ||
                existing is not Dictionary<string, AiStepExecutionMetadata> stepMap)
            {
                return null;
            }

            return stepMap.TryGetValue(stepName, out var metadata)
                ? metadata
                : null;
        }

        /// <summary>
        /// Begins a new execution attempt.
        /// </summary>
        private static void BeginAttempt(AiStepExecutionMetadata metadata)
        {
            metadata.AttemptCount++;
            metadata.Status = AiStepExecutionStatus.Running;
            metadata.LastStartedAtUtc = DateTimeOffset.UtcNow;
            metadata.FirstStartedAtUtc ??= metadata.LastStartedAtUtc;
        }

        /// <summary>
        /// Marks execution as successful.
        /// </summary>
        private static void MarkSucceeded(AiStepExecutionMetadata metadata)
        {
            metadata.Status = AiStepExecutionStatus.Completed;
            metadata.CompletedAtUtc = DateTimeOffset.UtcNow;
            metadata.LastError = null;
            metadata.LastExceptionType = null;
        }

        /// <summary>
        /// Determines whether a retry should occur.
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
        /// Computes retry delay.
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
        /// Gets or creates step metadata.
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