using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.AI.Runtime.ControlPlane.RuntimeQueue
{
    /// <summary>
    /// Runtime implementation of the local runtime queue control-plane facade.
    ///
    /// This class wraps the existing local runtime pipeline background controller.
    ///
    /// Important:
    /// This class does not replace local queues, workers, runtime instances,
    /// or DAG execution logic. It only exposes adapter-neutral control-plane
    /// operations over the local runtime queue owned by one runtime instance.
    /// </summary>
    public sealed class AiRuntimeQueueControlPlane : IAiRuntimeQueueControlPlane
    {
        private readonly IAiRuntimePipelineBackgroundController _controller;
        private readonly AiRuntimeQueueControlPlaneOptions _options;
        private readonly IAiControlPlaneObserver _observer;

        public AiRuntimeQueueControlPlane(
            IAiRuntimePipelineBackgroundController controller,
            IOptions<AiRuntimeQueueControlPlaneOptions> options,
            IAiControlPlaneObserver observer)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public Task<AiRuntimeQueueControlPlaneResult> ExecuteAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request.Operation switch
            {
                AiRuntimeQueueControlPlaneOperation.EnqueueRun => EnqueueRunAsync(request, cancellationToken),
                AiRuntimeQueueControlPlaneOperation.CancelRun => CancelRunAsync(request, cancellationToken),
                AiRuntimeQueueControlPlaneOperation.CancelQueuedRun => CancelQueuedRunAsync(request, cancellationToken),
                AiRuntimeQueueControlPlaneOperation.PauseQueue => PauseQueueAsync(request, cancellationToken),
                AiRuntimeQueueControlPlaneOperation.ResumeQueue => ResumeQueueAsync(request, cancellationToken),
                AiRuntimeQueueControlPlaneOperation.GetRunStatus => GetRunStatusAsync(request, cancellationToken),
                AiRuntimeQueueControlPlaneOperation.GetQueueStatus => GetQueueStatusAsync(request, cancellationToken),

                _ => throw new NotSupportedException(
                    $"Runtime queue control-plane operation '{request.Operation}' is not supported.")
            };
        }

        public Task<AiRuntimeQueueControlPlaneResult> EnqueueRunAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteQueueOperationAsync(
                request,
                AiRuntimeQueueControlPlaneOperation.EnqueueRun,
                cancellationToken);
        }

        public Task<AiRuntimeQueueControlPlaneResult> CancelRunAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteQueueOperationAsync(
                request,
                AiRuntimeQueueControlPlaneOperation.CancelRun,
                cancellationToken);
        }

        public Task<AiRuntimeQueueControlPlaneResult> CancelQueuedRunAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteQueueOperationAsync(
                request,
                AiRuntimeQueueControlPlaneOperation.CancelQueuedRun,
                cancellationToken);
        }

        public Task<AiRuntimeQueueControlPlaneResult> PauseQueueAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteQueueOperationAsync(
                request,
                AiRuntimeQueueControlPlaneOperation.PauseQueue,
                cancellationToken);
        }

        public Task<AiRuntimeQueueControlPlaneResult> ResumeQueueAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteQueueOperationAsync(
                request,
                AiRuntimeQueueControlPlaneOperation.ResumeQueue,
                cancellationToken);
        }

        public Task<AiRuntimeQueueControlPlaneResult> GetRunStatusAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteQueueOperationAsync(
                request,
                AiRuntimeQueueControlPlaneOperation.GetRunStatus,
                cancellationToken);
        }

        public Task<AiRuntimeQueueControlPlaneResult> GetQueueStatusAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteQueueOperationAsync(
                request,
                AiRuntimeQueueControlPlaneOperation.GetQueueStatus,
                cancellationToken);
        }

        private async Task<AiRuntimeQueueControlPlaneResult> ExecuteQueueOperationAsync(
            AiRuntimeQueueControlPlaneRequest request,
            AiRuntimeQueueControlPlaneOperation operation,
            CancellationToken cancellationToken)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            var correlation = CreateCorrelation(request);

            try
            {
                ValidateRequest(request, operation);
                EnsureEnabled(operation);

                await RecordStartedAsync(
                    request,
                    operation,
                    correlation,
                    cancellationToken).ConfigureAwait(false);

                var operationResult = await ExecuteInnerAsync(
                    request,
                    operation,
                    cancellationToken).ConfigureAwait(false);

                var completedAtUtc = DateTimeOffset.UtcNow;
                var durationMs = CalculateDurationMs(startedAtUtc, completedAtUtc);

                await RecordCompletedAsync(
                    request,
                    operation,
                    correlation,
                    operationResult,
                    durationMs,
                    cancellationToken).ConfigureAwait(false);

                return new AiRuntimeQueueControlPlaneResult
                {
                    Operation = operation,
                    Success = true,
                    Message = $"Runtime queue control-plane operation '{operation}' completed successfully.",
                    RunId = operationResult.RunHandle?.RunId ?? operationResult.RunState?.RunId ?? request.RunId,
                    ExecutionId = operationResult.RunHandle?.ExecutionId ?? operationResult.RunState?.ExecutionId,
                    RunHandle = operationResult.RunHandle,
                    RunState = request.IncludeRunState ? operationResult.RunState : null,
                    QueueState = request.IncludeQueueState ? operationResult.QueueState : null,
                    CorrelationId = correlation.CorrelationId,
                    RuntimeInstanceId =
                        operationResult.QueueState?.RuntimeInstanceId ??
                        operationResult.RunState?.RuntimeInstanceId ??
                        request.RuntimeInstanceId,
                    RequestedBy = request.RequestedBy,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = durationMs
                };
            }
            catch (Exception exception) when (_options.ReturnFailureResultInsteadOfThrowing)
            {
                var completedAtUtc = DateTimeOffset.UtcNow;
                var durationMs = CalculateDurationMs(startedAtUtc, completedAtUtc);

                await RecordFailedAsync(
                    request,
                    operation,
                    correlation,
                    exception,
                    durationMs,
                    cancellationToken).ConfigureAwait(false);

                return new AiRuntimeQueueControlPlaneResult
                {
                    Operation = operation,
                    Success = false,
                    Message = $"Runtime queue control-plane operation '{operation}' failed.",
                    RunId = request?.RunId,
                    Diagnostics = request?.IncludeDiagnostics == true
                        ? new[] { exception.Message }
                        : Array.Empty<string>(),
                    CorrelationId = correlation.CorrelationId,
                    RuntimeInstanceId = request?.RuntimeInstanceId,
                    RequestedBy = request?.RequestedBy,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = durationMs,
                    FailureReason = exception.Message
                };
            }
        }

        private async Task<RuntimeQueueOperationResult> ExecuteInnerAsync(
            AiRuntimeQueueControlPlaneRequest request,
            AiRuntimeQueueControlPlaneOperation operation,
            CancellationToken cancellationToken)
        {
            return operation switch
            {
                AiRuntimeQueueControlPlaneOperation.EnqueueRun =>
                    await EnqueueInnerAsync(request, cancellationToken).ConfigureAwait(false),

                AiRuntimeQueueControlPlaneOperation.CancelRun =>
                    await CancelRunInnerAsync(request, cancellationToken).ConfigureAwait(false),

                AiRuntimeQueueControlPlaneOperation.CancelQueuedRun =>
                    await CancelQueuedRunInnerAsync(request, cancellationToken).ConfigureAwait(false),

                AiRuntimeQueueControlPlaneOperation.PauseQueue =>
                    await PauseQueueInnerAsync(request, cancellationToken).ConfigureAwait(false),

                AiRuntimeQueueControlPlaneOperation.ResumeQueue =>
                    await ResumeQueueInnerAsync(request, cancellationToken).ConfigureAwait(false),

                AiRuntimeQueueControlPlaneOperation.GetRunStatus =>
                    await GetRunStatusInnerAsync(request, cancellationToken).ConfigureAwait(false),

                AiRuntimeQueueControlPlaneOperation.GetQueueStatus =>
                    await GetQueueStatusInnerAsync(cancellationToken).ConfigureAwait(false),

                _ => throw new NotSupportedException(
                    $"Runtime queue control-plane operation '{operation}' is not supported.")
            };
        }

        private async Task<RuntimeQueueOperationResult> EnqueueInnerAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken)
        {
            var handle = await _controller
                .EnqueueAsync(request.RunRequest!, cancellationToken)
                .ConfigureAwait(false);

            var runState = await _controller
                .GetRunStateAsync(handle.RunId, cancellationToken)
                .ConfigureAwait(false);

            var queueState = await _controller
                .GetQueueStateAsync(cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeQueueOperationResult
            {
                RunHandle = handle,
                RunState = runState,
                QueueState = queueState
            };
        }

        private async Task<RuntimeQueueOperationResult> CancelRunInnerAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken)
        {
            await _controller
                .CancelRunAsync(
                    request.RunId!,
                    request.Reason,
                    request.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);

            var runState = await _controller
                .GetRunStateAsync(request.RunId!, cancellationToken)
                .ConfigureAwait(false);

            var queueState = await _controller
                .GetQueueStateAsync(cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeQueueOperationResult
            {
                RunState = runState,
                QueueState = queueState
            };
        }

        private async Task<RuntimeQueueOperationResult> CancelQueuedRunInnerAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken)
        {
            await _controller
                .CancelQueuedRunAsync(
                    request.RunId!,
                    request.Reason,
                    request.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);

            var runState = await _controller
                .GetRunStateAsync(request.RunId!, cancellationToken)
                .ConfigureAwait(false);

            var queueState = await _controller
                .GetQueueStateAsync(cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeQueueOperationResult
            {
                RunState = runState,
                QueueState = queueState
            };
        }

        private async Task<RuntimeQueueOperationResult> PauseQueueInnerAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken)
        {
            await _controller
                .PauseQueueAsync(
                    request.Reason,
                    request.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);

            var queueState = await _controller
                .GetQueueStateAsync(cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeQueueOperationResult
            {
                QueueState = queueState
            };
        }

        private async Task<RuntimeQueueOperationResult> ResumeQueueInnerAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken)
        {
            await _controller
                .ResumeQueueAsync(
                    request.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);

            var queueState = await _controller
                .GetQueueStateAsync(cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeQueueOperationResult
            {
                QueueState = queueState
            };
        }

        private async Task<RuntimeQueueOperationResult> GetRunStatusInnerAsync(
            AiRuntimeQueueControlPlaneRequest request,
            CancellationToken cancellationToken)
        {
            var runState = await _controller
                .GetRunStateAsync(request.RunId!, cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeQueueOperationResult
            {
                RunState = runState
            };
        }

        private async Task<RuntimeQueueOperationResult> GetQueueStatusInnerAsync(
            CancellationToken cancellationToken)
        {
            var queueState = await _controller
                .GetQueueStateAsync(cancellationToken)
                .ConfigureAwait(false);

            return new RuntimeQueueOperationResult
            {
                QueueState = queueState
            };
        }

        private static AiRuntimeExecutionCorrelationContext CreateCorrelation(
            AiRuntimeQueueControlPlaneRequest request)
        {
            return new AiRuntimeExecutionCorrelationContext
            {
                CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                    ? Guid.NewGuid().ToString("N")
                    : request.CorrelationId,

                RunId = request.RunId,
                RuntimeInstanceId = request.RuntimeInstanceId
            };
        }

        private async Task RecordStartedAsync(
            AiRuntimeQueueControlPlaneRequest request,
            AiRuntimeQueueControlPlaneOperation operation,
            AiRuntimeExecutionCorrelationContext correlation,
            CancellationToken cancellationToken)
        {
            await _observer.RecordAsync(
                new AiControlPlaneEvent
                {
                    EventType = AiControlPlaneEventType.OperationStarted,
                    Area = AiControlPlaneArea.RunControl,
                    Operation = operation.ToString(),
                    Correlation = correlation,
                    Message = $"Runtime queue control-plane operation '{operation}' started.",
                    Properties = new Dictionary<string, object?>
                    {
                        ["source"] = request.Source,
                        ["requestedBy"] = request.RequestedBy,
                        ["reason"] = request.Reason,
                        ["runId"] = request.RunId,
                        ["hasRunRequest"] = request.RunRequest is not null,
                        ["includeRunState"] = request.IncludeRunState,
                        ["includeQueueState"] = request.IncludeQueueState,
                        ["includeDiagnostics"] = request.IncludeDiagnostics
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        private async Task RecordCompletedAsync(
            AiRuntimeQueueControlPlaneRequest request,
            AiRuntimeQueueControlPlaneOperation operation,
            AiRuntimeExecutionCorrelationContext correlation,
            RuntimeQueueOperationResult operationResult,
            long durationMs,
            CancellationToken cancellationToken)
        {
            await _observer.RecordAsync(
                new AiControlPlaneEvent
                {
                    EventType = AiControlPlaneEventType.OperationCompleted,
                    Area = AiControlPlaneArea.RunControl,
                    Operation = operation.ToString(),
                    Outcome = AiControlPlaneOperationOutcome.Succeeded,
                    Correlation = correlation,
                    DurationMs = durationMs,
                    Message = $"Runtime queue control-plane operation '{operation}' completed successfully.",
                    Properties = new Dictionary<string, object?>
                    {
                        ["source"] = request.Source,
                        ["requestedBy"] = request.RequestedBy,
                        ["runId"] = operationResult.RunHandle?.RunId ?? operationResult.RunState?.RunId ?? request.RunId,
                        ["executionId"] = operationResult.RunHandle?.ExecutionId ?? operationResult.RunState?.ExecutionId,
                        ["queuePaused"] = operationResult.QueueState?.IsPaused,
                        ["queuedRunCount"] = operationResult.QueueState?.QueuedRunCount,
                        ["runningRunCount"] = operationResult.QueueState?.RunningRunCount,
                        ["availableRunSlots"] = operationResult.QueueState?.AvailableRunSlots
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        private async Task RecordFailedAsync(
            AiRuntimeQueueControlPlaneRequest? request,
            AiRuntimeQueueControlPlaneOperation operation,
            AiRuntimeExecutionCorrelationContext correlation,
            Exception exception,
            long durationMs,
            CancellationToken cancellationToken)
        {
            await _observer.RecordAsync(
                new AiControlPlaneEvent
                {
                    EventType = AiControlPlaneEventType.OperationFailed,
                    Area = AiControlPlaneArea.RunControl,
                    Operation = operation.ToString(),
                    Outcome = AiControlPlaneOperationOutcome.Failed,
                    Correlation = correlation,
                    DurationMs = durationMs,
                    Message = $"Runtime queue control-plane operation '{operation}' failed.",
                    FailureReason = exception.Message,
                    Properties = new Dictionary<string, object?>
                    {
                        ["source"] = request?.Source,
                        ["requestedBy"] = request?.RequestedBy,
                        ["runId"] = request?.RunId,
                        ["exceptionType"] = exception.GetType().Name
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        private static void ValidateRequest(
            AiRuntimeQueueControlPlaneRequest request,
            AiRuntimeQueueControlPlaneOperation operation)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (operation == AiRuntimeQueueControlPlaneOperation.EnqueueRun &&
                request.RunRequest is null)
            {
                throw new ArgumentException(
                    "RunRequest is required for EnqueueRun operations.",
                    nameof(request));
            }

            if (RequiresRunId(operation) &&
                string.IsNullOrWhiteSpace(request.RunId))
            {
                throw new ArgumentException(
                    "RunId is required for this runtime queue control-plane operation.",
                    nameof(request));
            }
        }

        private static bool RequiresRunId(
            AiRuntimeQueueControlPlaneOperation operation)
        {
            return operation is
                AiRuntimeQueueControlPlaneOperation.CancelRun or
                AiRuntimeQueueControlPlaneOperation.CancelQueuedRun or
                AiRuntimeQueueControlPlaneOperation.GetRunStatus;
        }

        private void EnsureEnabled(
            AiRuntimeQueueControlPlaneOperation operation)
        {
            var enabled = operation switch
            {
                AiRuntimeQueueControlPlaneOperation.EnqueueRun => _options.EnableEnqueueRun,
                AiRuntimeQueueControlPlaneOperation.CancelRun => _options.EnableCancelRun,
                AiRuntimeQueueControlPlaneOperation.CancelQueuedRun => _options.EnableCancelQueuedRun,
                AiRuntimeQueueControlPlaneOperation.PauseQueue => _options.EnablePauseQueue,
                AiRuntimeQueueControlPlaneOperation.ResumeQueue => _options.EnableResumeQueue,
                AiRuntimeQueueControlPlaneOperation.GetRunStatus => _options.EnableGetRunStatus,
                AiRuntimeQueueControlPlaneOperation.GetQueueStatus => _options.EnableGetQueueStatus,
                _ => false
            };

            if (!enabled)
            {
                throw new InvalidOperationException(
                    $"Runtime queue control-plane operation '{operation}' is disabled.");
            }
        }

        private long CalculateDurationMs(
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            if (!_options.MeasureDuration)
            {
                return 0;
            }

            return (long)(completedAtUtc - startedAtUtc).TotalMilliseconds;
        }

        private sealed class RuntimeQueueOperationResult
        {
            public AiRuntimeWorkerRunHandle? RunHandle { get; init; }

            public AiRuntimePipelineRunState? RunState { get; init; }

            public AiRuntimePipelineQueueState? QueueState { get; init; }
        }
    }
}