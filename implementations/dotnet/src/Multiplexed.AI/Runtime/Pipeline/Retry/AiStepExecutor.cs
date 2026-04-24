using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
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
    /// PAYLOAD EVOLUTION:
    /// - Step results may now receive a payload representation through <see cref="IAiExecutionDataPolicy"/>
    /// - Small values remain inline as before
    /// - Large values may be externalized by the policy and replaced with a compact inline summary
    /// - The goal is to reduce ledger/state size without breaking execution, retry, replay, or bindings
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
        private readonly IAiExecutionDataPolicy _dataPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepExecutor"/> class.
        /// </summary>
        public AiStepExecutor(
            IAiRetryExceptionClassifier exceptionClassifier,
            IAiRuntimeLogger logger,
            IAiExecutionDataPolicy dataPolicy)
        {
            ArgumentNullException.ThrowIfNull(exceptionClassifier);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(dataPolicy);

            _exceptionClassifier = exceptionClassifier;
            _logger = logger;
            _dataPolicy = dataPolicy;
        }

        /// <summary>
        /// Executes the specified resolved step using local attribute-driven retry behavior.
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

                    await AttachPayloadAsync(result, cancellationToken);

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
        /// Attaches payload representations to the step result.
        ///
        /// BEHAVIOR:
        /// - Processes primary Value (already implemented)
        /// - Processes structured Data entries (NEW)
        ///
        /// DESIGN:
        /// - Large values are externalized
        /// - Inline values are preserved
        /// - Data entries remain accessible via existing APIs
        ///
        /// IMPORTANT:
        /// - No breaking changes
        /// - Data dictionary is preserved (only values replaced with summary when needed)
        /// - DataPayloads holds real payload references
        /// </summary>
        private async Task AttachPayloadAsync(
            AiStepResult result,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(result);

            // ---------------------------------------------------------------------
            // PRIMARY VALUE (EXISTING LOGIC)
            // ---------------------------------------------------------------------
            if (result.Value is not null && result.Payload == null)
            {
                var originalValue = result.Value;

                var payload = await _dataPolicy.StoreAsync(
                    originalValue,
                    cancellationToken);

                result.Payload = payload;

                if (!payload.IsInline)
                {
                    result.Value = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["payloadExternalized"] = true,
                        ["artifactId"] = payload.ArtifactId,
                        ["contentHash"] = payload.ContentHash,
                        ["sizeBytes"] = payload.SizeBytes,
                        ["contentType"] = payload.ContentType
                    };
                }
            }

            // ---------------------------------------------------------------------
            // STRUCTURED DATA 
            // ---------------------------------------------------------------------
            if (result.Data == null || result.Data.Count == 0)
                return;

            foreach (var entry in result.Data.ToList()) // copy to avoid mutation issues
            {
                var key = entry.Key;
                var value = entry.Value;

                if (value is null)
                    continue;

                var payload = await _dataPolicy.StoreAsync(value, cancellationToken);

                if (payload.IsInline)
                    continue;

                // initialize payload dictionary
                result.DataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);

                result.DataPayloads[key] = payload;

                // replace inline value with compact summary
                result.Data[key] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["payloadExternalized"] = true,
                    ["artifactId"] = payload.ArtifactId,
                    ["contentHash"] = payload.ContentHash,
                    ["sizeBytes"] = payload.SizeBytes,
                    ["contentType"] = payload.ContentType
                };
            }
        }

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

        private static void BeginAttempt(AiStepExecutionMetadata metadata)
        {
            metadata.AttemptCount++;
            metadata.Status = AiStepExecutionStatus.Running;
            metadata.LastStartedAtUtc = DateTimeOffset.UtcNow;
            metadata.FirstStartedAtUtc ??= metadata.LastStartedAtUtc;
        }

        private static void MarkSucceeded(AiStepExecutionMetadata metadata)
        {
            metadata.Status = AiStepExecutionStatus.Completed;
            metadata.CompletedAtUtc = DateTimeOffset.UtcNow;
            metadata.LastError = null;
            metadata.LastExceptionType = null;
        }

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