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
    /// Executes a single resolved AI step with local, in-process retry behavior.
    ///
    /// PURPOSE:
    /// - Reads retry intent from <see cref="AiRetryPolicyAttribute"/>
    /// - Classifies retryable exceptions using <see cref="IAiRetryExceptionClassifier"/>
    /// - Tracks local execution attempts through <see cref="AiStepExecutionMetadata"/>
    /// - Prevents re-execution of steps already marked as completed in local metadata
    /// - Emits structured execution events for observability
    ///
    /// IMPORTANT:
    /// - This class is scoped to execution of ONE resolved step
    /// - It does not own pipeline orchestration
    /// - It does not own distributed retry coordination
    /// - Durable DAG retry state remains owned by <see cref="AiStepState"/> and the execution store
    ///
    /// SEMANTICS:
    /// - <see cref="AiStepExecutionMetadata.AttemptCount"/> counts local in-process attempts
    /// - It is NOT equivalent to distributed <see cref="AiStepState.RetryCount"/>
    /// - This executor may retry inside the same process before control returns to orchestration
    /// </summary>
    public sealed class AiStepExecutor : IAiStepExecutor
    {
        private readonly IAiRetryExceptionClassifier _exceptionClassifier;
        private readonly IAiRuntimeLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepExecutor"/> class.
        /// </summary>
        /// <param name="exceptionClassifier">
        /// Classifies exceptions to determine whether they are retryable.
        /// </param>
        /// <param name="logger">
        /// Runtime logger used to emit structured step execution events.
        /// </param>
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
        /// Executes the specified resolved step using local attribute-driven retry behavior.
        ///
        /// FLOW:
        /// - Resolve retry policy from the concrete step type
        /// - Load or create local step execution metadata
        /// - Skip execution if local metadata already marks the step as completed
        /// - Execute the step
        /// - Retry locally when policy allows and the exception is retryable
        /// - Return the final <see cref="AiStepResult"/> or rethrow the terminal exception
        ///
        /// IMPORTANT:
        /// - This method performs only local retry within the current process
        /// - It does not schedule durable DAG retry windows
        /// - It does not mutate distributed retry counters in <see cref="AiStepState"/>
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

            // Primary runtime identity comes from the resolved pipeline step name.
            // Fallback type name support is kept for compatibility with tests or
            // older metadata that may have used the concrete type name instead.
            var stepName = resolvedStep.Name;
            var fallbackStepName = stepType.Name;

            var retryPolicy = stepType.GetCustomAttribute<AiRetryPolicyAttribute>(inherit: true);

            // Attempt to reuse existing local step execution metadata.
            // We first try the resolved logical step name, then the fallback type name
            // for compatibility with older or test-oriented metadata keys.
            var metadata =
                TryGetStepMetadata(context.State, stepName)
                ?? TryGetStepMetadata(context.State, fallbackStepName);

            // Local idempotency safeguard:
            // if this step has already completed successfully in local execution metadata,
            // skip re-execution and return a successful no-op result.
            if (metadata != null && metadata.IsCompleted)
            {
                _logger.StepExecutor.Skipped(context.ExecutionId, stepName);

                return AiStepResult.Ok(
                    output: $"Step '{stepName}' was skipped because it had already completed successfully.");
            }

            // Always create or bind metadata using the primary logical step identity.
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
        /// Attempts to retrieve existing local step execution metadata without creating it.
        ///
        /// Returns <c>null</c> when:
        /// - no step execution metadata bag exists
        /// - the bag exists but has an unexpected type
        /// - the specified step key is not present
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
        /// Begins a new local execution attempt.
        ///
        /// This method:
        /// - increments <see cref="AiStepExecutionMetadata.AttemptCount"/>
        /// - marks metadata status as <see cref="AiStepExecutionStatus.Running"/>
        /// - records the latest attempt start time
        /// - preserves the first attempt start time once set
        /// </summary>
        private static void BeginAttempt(AiStepExecutionMetadata metadata)
        {
            metadata.AttemptCount++;
            metadata.Status = AiStepExecutionStatus.Running;
            metadata.LastStartedAtUtc = DateTimeOffset.UtcNow;
            metadata.FirstStartedAtUtc ??= metadata.LastStartedAtUtc;
        }

        /// <summary>
        /// Marks the local execution metadata as successfully completed.
        ///
        /// This method:
        /// - sets status to <see cref="AiStepExecutionStatus.Completed"/>
        /// - records completion time
        /// - clears the last error and exception type
        /// </summary>
        private static void MarkSucceeded(AiStepExecutionMetadata metadata)
        {
            metadata.Status = AiStepExecutionStatus.Completed;
            metadata.CompletedAtUtc = DateTimeOffset.UtcNow;
            metadata.LastError = null;
            metadata.LastExceptionType = null;
        }

        /// <summary>
        /// Determines whether the current exception should trigger another local retry attempt.
        ///
        /// RULES:
        /// - if no retry policy is declared, retry is disabled
        /// - if the current local attempt count exceeds the configured max retry count, retry is disabled
        /// - if <see cref="AiRetryPolicyAttribute.RetryTransientOnly"/> is false, all exceptions are retryable
        /// - otherwise exception retryability is delegated to <see cref="IAiRetryExceptionClassifier"/>
        ///
        /// IMPORTANT:
        /// - <paramref name="attemptCount"/> is the local in-process attempt count
        /// - it is not the same thing as durable DAG retry count
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
        /// Computes the delay before the next local retry attempt.
        ///
        /// Supported modes:
        /// - <see cref="AiRetryBackoffMode.Fixed"/>
        /// - <see cref="AiRetryBackoffMode.Exponential"/>
        ///
        /// IMPORTANT:
        /// - delay is computed from local retry policy only
        /// - this does not schedule durable retry windows in execution state
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
        /// Gets the local step execution metadata map or creates it when missing,
        /// then returns the entry for the specified logical step name.
        ///
        /// If the step metadata entry does not yet exist, a new one is created.
        /// Updates <see cref="AiExecutionState.UpdatedAtUtc"/>.
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