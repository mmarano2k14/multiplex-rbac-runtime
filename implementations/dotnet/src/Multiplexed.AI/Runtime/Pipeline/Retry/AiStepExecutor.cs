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
    /// - It does not own payload compaction
    /// - It does not own consolidated memory writing
    /// - Durable DAG retry state remains owned by <see cref="AiStepState"/> and the execution store
    ///
    /// CENTRALIZATION NOTE:
    /// - Payload compaction is handled centrally by the DAG engine before result persistence
    /// - Memory writing should also be handled centrally by orchestration-level runtime services
    /// - Keeping this executor focused on local retry avoids inconsistent behavior between
    ///   sequential, DAG, RAG, operation, and provider execution paths
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
        /// - It does not compact payloads; orchestration-level code must do that before persistence
        /// - It does not write consolidated memory; orchestration-level code should own that
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

            var stepName = resolvedStep.Name;
            var fallbackStepName = stepType.Name;

            var retryPolicy = stepType.GetCustomAttribute<AiRetryPolicyAttribute>(inherit: true);

            var metadata =
                TryGetStepMetadata(context.State, stepName)
                ?? TryGetStepMetadata(context.State, fallbackStepName);

            if (metadata != null && metadata.IsCompleted)
            {
                _logger.StepExecutor.Skipped(context.ExecutionId, stepName);

                return AiStepResult.Ok(
                    output: $"Step '{stepName}' was skipped because it had already completed successfully.");
            }

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