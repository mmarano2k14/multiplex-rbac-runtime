using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Memory;
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
    /// - Step results may receive a payload representation through <see cref="IAiExecutionDataPolicy"/>
    /// - Small values remain inline as before
    /// - Large values may be externalized by the policy and replaced with compact inline summaries
    /// - The goal is to reduce ledger/state size without breaking execution, retry, replay, or bindings
    ///
    /// MEMORY EVOLUTION:
    /// - Successful step results may be written to consolidated memory through <see cref="IAiMemoryWriter"/>
    /// - Memory writing is optional and best-effort
    /// - Consolidated memory is derived knowledge, not execution truth
    /// - Memory writer failure must never fail the step execution
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
        private readonly IAiMemoryWriter? _memoryWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepExecutor"/> class.
        ///
        /// COMPATIBILITY:
        /// - <paramref name="memoryWriter"/> is optional to preserve all existing tests and registrations
        /// - When no memory writer is provided, execution behavior remains unchanged
        /// </summary>
        public AiStepExecutor(
            IAiRetryExceptionClassifier exceptionClassifier,
            IAiRuntimeLogger logger,
            IAiExecutionDataPolicy dataPolicy,
            IAiMemoryWriter? memoryWriter = null)
        {
            ArgumentNullException.ThrowIfNull(exceptionClassifier);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(dataPolicy);

            _exceptionClassifier = exceptionClassifier;
            _logger = logger;
            _dataPolicy = dataPolicy;
            _memoryWriter = memoryWriter;
        }

        /// <summary>
        /// Executes the specified resolved step using local attribute-driven retry behavior.
        ///
        /// FLOW:
        /// - Resolve retry policy from the concrete step type
        /// - Load or create local step execution metadata
        /// - Skip execution if local metadata already marks the step as completed
        /// - Execute the step
        /// - Attach payload representations for large primary values and structured data entries
        /// - Optionally write consolidated memory from the step result
        /// - Retry locally when policy allows and the exception is retryable
        /// - Return the final <see cref="AiStepResult"/> or rethrow the terminal exception
        ///
        /// IMPORTANT:
        /// - This method performs only local retry within the current process
        /// - It does not schedule durable DAG retry windows
        /// - It does not mutate distributed retry counters in <see cref="AiStepState"/>
        /// - Memory writing is best-effort and must never affect execution correctness
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

                    await WriteMemoryAsync(
                        context,
                        stepName,
                        result,
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
        /// Attaches payload representations to the step result.
        ///
        /// BEHAVIOR:
        /// - Processes the primary <see cref="AiStepResult.Value"/>
        /// - Processes structured <see cref="AiStepResult.Data"/> entries
        ///
        /// DESIGN:
        /// - Large values are externalized through <see cref="IAiExecutionDataPolicy"/>
        /// - Inline values are preserved
        /// - Externalized values are replaced with compact summaries
        /// - Full payloads remain resolvable through <see cref="AiStepResult.Payload"/>
        ///   or <see cref="AiStepResult.DataPayloads"/>
        ///
        /// IMPORTANT:
        /// - No step success/failure semantics are changed
        /// - The Data dictionary is preserved
        /// - Only values that the policy externalizes are replaced by summaries
        /// - This method does not mutate retry metadata or distributed DAG state
        /// </summary>
        private async Task AttachPayloadAsync(
            AiStepResult result,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(result);

            // ---------------------------------------------------------------------
            // PRIMARY VALUE
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
                    result.Value = CreatePayloadSummary(payload);
                }
            }

            // ---------------------------------------------------------------------
            // STRUCTURED DATA
            // ---------------------------------------------------------------------
            if (result.Data == null || result.Data.Count == 0)
                return;

            foreach (var entry in result.Data.ToList())
            {
                var key = entry.Key;
                var value = entry.Value;

                if (value is null)
                    continue;

                var payload = await _dataPolicy.StoreAsync(
                    value,
                    cancellationToken);

                if (payload.IsInline)
                    continue;

                result.DataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);
                result.DataPayloads[key] = payload;

                result.Data[key] = CreatePayloadSummary(payload);
            }
        }

        /// <summary>
        /// Writes a consolidated memory from the step result when memory writing is enabled.
        ///
        /// PURPOSE:
        /// - Allows successful step outputs to become long-term consolidated memory
        /// - Keeps memory creation separate from DAG execution, retry, and replay
        /// - Treats memory as a derived artifact, not as execution truth
        ///
        /// IMPORTANT:
        /// - This method is best-effort by design
        /// - Memory writer failures must never fail the step execution
        /// - Consolidated memory must never be required for deterministic replay
        /// - Failed step results are passed to the writer, but the default writer ignores them
        /// </summary>
        private async Task WriteMemoryAsync(
            AiStepExecutionContext context,
            string stepName,
            AiStepResult result,
            CancellationToken cancellationToken)
        {
            if (_memoryWriter is null)
                return;

            try
            {
                var scope = !string.IsNullOrWhiteSpace(context.State.PipelineName)
                    ? context.State.PipelineName!
                    : "default";

                await _memoryWriter.WriteFromStepResultAsync(
                    context.Record,
                    stepName,
                    result,
                    scope,
                    cancellationToken);
            }
            catch
            {
                // Memory is derived and best-effort.
                // It must never break execution, retry, DAG convergence, or replay.
            }
        }

        /// <summary>
        /// Creates the compact inline summary used when a payload is externalized.
        ///
        /// PURPOSE:
        /// - Keeps the execution state small
        /// - Preserves enough metadata for diagnostics and traceability
        /// - Avoids leaving large duplicated values in the ledger/state
        ///
        /// IMPORTANT:
        /// - This summary is not the payload itself
        /// - Full content must be resolved through the stored payload reference
        /// </summary>
        private static Dictionary<string, object?> CreatePayloadSummary(
            AiStoredPayload payload)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["payloadExternalized"] = true,
                ["artifactId"] = payload.ArtifactId,
                ["contentHash"] = payload.ContentHash,
                ["sizeBytes"] = payload.SizeBytes,
                ["contentType"] = payload.ContentType
            };
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