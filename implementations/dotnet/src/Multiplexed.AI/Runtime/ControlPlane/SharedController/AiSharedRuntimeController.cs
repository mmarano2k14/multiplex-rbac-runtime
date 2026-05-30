using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.Admission;
using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.Abstractions.AI.Observability.Context;
using System.Collections.Concurrent;

namespace Multiplexed.AI.Runtime.ControlPlane.SharedController
{
    /// <summary>
    /// In-memory V1 implementation of the shared runtime controller.
    /// </summary>
    /// <remarks>
    /// The shared runtime controller sits above run admission, runtime instances,
    /// local runtime queue control, the future shared queue, and the future
    /// Kubernetes scale-out adapter.
    ///
    /// V1 intentionally does not dispatch to remote runtime instances yet.
    /// It records the admission decision and shared run status so the control-plane
    /// lifecycle is visible and testable before adding Redis/shared queue behavior.
    ///
    /// Important:
    /// This class does not execute DAG steps.
    /// It does not claim work.
    /// It does not directly create Kubernetes pods.
    /// It does not replace local runtime queues.
    /// </remarks>
    public sealed class AiSharedRuntimeController : IAiSharedRuntimeController
    {
        private readonly IAiRunAdmissionController _admissionController;
        private readonly AiSharedRuntimeControllerOptions _options;
        private readonly IAiControlPlaneObserver _observer;
        private readonly ConcurrentDictionary<string, AiSharedRunRecord> _runs =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="AiSharedRuntimeController"/> class.
        /// </summary>
        /// <param name="admissionController">The run admission controller.</param>
        /// <param name="options">The shared runtime controller options.</param>
        /// <param name="observer">The control-plane observer used to record operation events.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="admissionController"/>, <paramref name="options"/>,
        /// or <paramref name="observer"/> is null.
        /// </exception>
        public AiSharedRuntimeController(
            IAiRunAdmissionController admissionController,
            IOptions<AiSharedRuntimeControllerOptions> options,
            IAiControlPlaneObserver observer)
        {
            _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        /// <inheritdoc />
        public Task<AiSharedRuntimeControllerResult> ExecuteAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request.Operation switch
            {
                AiSharedRuntimeControllerOperation.SubmitRun => SubmitRunAsync(request, cancellationToken),
                AiSharedRuntimeControllerOperation.GetRun => GetRunAsync(request, cancellationToken),
                AiSharedRuntimeControllerOperation.ListRuns => ListRunsAsync(request, cancellationToken),
                AiSharedRuntimeControllerOperation.CancelRun => CancelRunAsync(request, cancellationToken),

                _ => throw new NotSupportedException(
                    $"Shared runtime controller operation '{request.Operation}' is not supported.")
            };
        }

        /// <inheritdoc />
        public Task<AiSharedRuntimeControllerResult> SubmitRunAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteControllerOperationAsync(
                request,
                AiSharedRuntimeControllerOperation.SubmitRun,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<AiSharedRuntimeControllerResult> GetRunAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteControllerOperationAsync(
                request,
                AiSharedRuntimeControllerOperation.GetRun,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<AiSharedRuntimeControllerResult> ListRunsAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteControllerOperationAsync(
                request,
                AiSharedRuntimeControllerOperation.ListRuns,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<AiSharedRuntimeControllerResult> CancelRunAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteControllerOperationAsync(
                request,
                AiSharedRuntimeControllerOperation.CancelRun,
                cancellationToken);
        }

        /// <summary>
        /// Executes one shared runtime controller operation with validation,
        /// observability, duration measurement, and structured failure handling.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="operation">The shared runtime controller operation to execute.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The shared runtime controller result.</returns>
        private async Task<AiSharedRuntimeControllerResult> ExecuteControllerOperationAsync(
            AiSharedRuntimeControllerRequest request,
            AiSharedRuntimeControllerOperation operation,
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

                return new AiSharedRuntimeControllerResult
                {
                    Operation = operation,
                    Success = true,
                    Message = $"Shared runtime controller operation '{operation}' completed successfully.",
                    SharedRunId =
                        operationResult.Run?.SharedRunId ??
                        request.SharedRunId ??
                        request.RequestedSharedRunId,
                    LocalRunId = operationResult.Run?.LocalRunId,
                    ExecutionId = operationResult.Run?.ExecutionId,
                    AssignedRuntimeInstanceId = operationResult.Run?.AssignedRuntimeInstanceId,
                    Run = operationResult.Run,
                    Runs = operationResult.Runs,
                    CorrelationId = correlation.CorrelationId,
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

                return new AiSharedRuntimeControllerResult
                {
                    Operation = operation,
                    Success = false,
                    Message = $"Shared runtime controller operation '{operation}' failed.",
                    SharedRunId = request?.SharedRunId ?? request?.RequestedSharedRunId,
                    Diagnostics = request?.IncludeDiagnostics == true
                        ? new[] { exception.Message }
                        : Array.Empty<string>(),
                    CorrelationId = correlation.CorrelationId,
                    RequestedBy = request?.RequestedBy,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = durationMs,
                    FailureReason = exception.Message
                };
            }
        }

        /// <summary>
        /// Dispatches the shared controller operation to the matching internal handler.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="operation">The shared runtime controller operation to execute.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The internal shared controller operation result.</returns>
        private async Task<SharedRuntimeControllerOperationResult> ExecuteInnerAsync(
            AiSharedRuntimeControllerRequest request,
            AiSharedRuntimeControllerOperation operation,
            CancellationToken cancellationToken)
        {
            return operation switch
            {
                AiSharedRuntimeControllerOperation.SubmitRun =>
                    await SubmitRunInnerAsync(request, cancellationToken).ConfigureAwait(false),

                AiSharedRuntimeControllerOperation.GetRun =>
                    GetRunInner(request),

                AiSharedRuntimeControllerOperation.ListRuns =>
                    ListRunsInner(request),

                AiSharedRuntimeControllerOperation.CancelRun =>
                    CancelRunInner(request),

                _ => throw new NotSupportedException(
                    $"Shared runtime controller operation '{operation}' is not supported.")
            };
        }

        /// <summary>
        /// Submits a run to the shared runtime controller and records the admission decision.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The internal shared controller operation result.</returns>
        private async Task<SharedRuntimeControllerOperationResult> SubmitRunInnerAsync(
            AiSharedRuntimeControllerRequest request,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var sharedRunId = string.IsNullOrWhiteSpace(request.RequestedSharedRunId)
                ? Guid.NewGuid().ToString("N")
                : request.RequestedSharedRunId;

            var admissionDecision = await _admissionController
                .AdmitAsync(
                    new AiRunAdmissionRequest
                    {
                        RunRequest = request.RunRequest!,
                        RunId = sharedRunId,
                        TenantId = request.TenantId,
                        PipelineKey = request.PipelineKey ?? request.RunRequest?.PipelineName,
                        PreferredRuntimeInstanceId = request.PreferredRuntimeInstanceId,
                        CorrelationId = request.CorrelationId,
                        RequestedBy = request.RequestedBy,
                        Source = request.Source,
                        Reason = request.Reason,
                        Metadata = request.Metadata
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var status = MapAdmissionDecisionToStatus(admissionDecision);
            var failureReason = admissionDecision.Rejected
                ? admissionDecision.Reason
                : null;

            var record = new AiSharedRunRecord
            {
                SharedRunId = sharedRunId,
                Status = status,
                RunRequest = request.RunRequest!,
                AssignedRuntimeInstanceId = admissionDecision.AssignedRuntimeInstanceId,
                AdmissionDecision = admissionDecision,
                TenantId = request.TenantId,
                PipelineKey = request.PipelineKey ?? request.RunRequest?.PipelineName,
                CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                    ? sharedRunId
                    : request.CorrelationId,
                RequestedBy = request.RequestedBy,
                Source = request.Source,
                Reason = request.Reason,
                FailureReason = failureReason,
                SubmittedAtUtc = now,
                UpdatedAtUtc = now,
                Metadata = CopyMetadata(request.Metadata)
            };

            if (!_runs.TryAdd(sharedRunId, record))
            {
                throw new InvalidOperationException(
                    $"Shared run '{sharedRunId}' already exists.");
            }

            return new SharedRuntimeControllerOperationResult
            {
                Run = record
            };
        }

        /// <summary>
        /// Gets a shared run record.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <returns>The internal shared controller operation result.</returns>
        private SharedRuntimeControllerOperationResult GetRunInner(
            AiSharedRuntimeControllerRequest request)
        {
            _runs.TryGetValue(request.SharedRunId!, out var record);

            return new SharedRuntimeControllerOperationResult
            {
                Run = record
            };
        }

        /// <summary>
        /// Lists shared run records known by the controller.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <returns>The internal shared controller operation result.</returns>
        private SharedRuntimeControllerOperationResult ListRunsInner(
            AiSharedRuntimeControllerRequest request)
        {
            var runs = _runs.Values
                .Where(run => request.IncludeCancelled || run.Status != AiSharedRunStatus.Cancelled)
                .Where(run => request.IncludeCompleted || run.Status != AiSharedRunStatus.Completed)
                .Where(run => request.IncludeFailed || run.Status != AiSharedRunStatus.Failed)
                .OrderBy(run => run.SubmittedAtUtc)
                .ThenBy(run => run.SharedRunId, StringComparer.Ordinal)
                .ToArray();

            return new SharedRuntimeControllerOperationResult
            {
                Runs = runs
            };
        }

        /// <summary>
        /// Cancels a shared run known by the controller.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <returns>The internal shared controller operation result.</returns>
        private SharedRuntimeControllerOperationResult CancelRunInner(
            AiSharedRuntimeControllerRequest request)
        {
            if (!_runs.TryGetValue(request.SharedRunId!, out var existing))
            {
                return new SharedRuntimeControllerOperationResult();
            }

            if (existing.Status is
                AiSharedRunStatus.Completed or
                AiSharedRunStatus.Failed or
                AiSharedRunStatus.Cancelled)
            {
                return new SharedRuntimeControllerOperationResult
                {
                    Run = existing
                };
            }

            var now = DateTimeOffset.UtcNow;

            var updated = new AiSharedRunRecord
            {
                SharedRunId = existing.SharedRunId,
                Status = AiSharedRunStatus.Cancelled,
                RunRequest = existing.RunRequest,
                LocalRunId = existing.LocalRunId,
                ExecutionId = existing.ExecutionId,
                AssignedRuntimeInstanceId = existing.AssignedRuntimeInstanceId,
                AdmissionDecision = existing.AdmissionDecision,
                TenantId = existing.TenantId,
                PipelineKey = existing.PipelineKey,
                CorrelationId = existing.CorrelationId,
                RequestedBy = request.RequestedBy ?? existing.RequestedBy,
                Source = request.Source ?? existing.Source,
                Reason = request.Reason ?? existing.Reason,
                FailureReason = request.Reason ?? "Shared run cancelled.",
                SubmittedAtUtc = existing.SubmittedAtUtc,
                UpdatedAtUtc = now,
                Metadata = existing.Metadata
            };

            _runs[request.SharedRunId!] = updated;

            return new SharedRuntimeControllerOperationResult
            {
                Run = updated
            };
        }

        /// <summary>
        /// Maps an admission decision to a shared run status.
        /// </summary>
        /// <param name="decision">The admission decision.</param>
        /// <returns>The shared run status.</returns>
        private static AiSharedRunStatus MapAdmissionDecisionToStatus(
            AiRunAdmissionDecision decision)
        {
            return decision.DecisionType switch
            {
                AiRunAdmissionDecisionType.AssignToInstance => AiSharedRunStatus.AssignedToInstance,
                AiRunAdmissionDecisionType.QueueGlobally => AiSharedRunStatus.QueuedGlobally,
                AiRunAdmissionDecisionType.RequestScaleOut => AiSharedRunStatus.ScaleOutRequested,
                AiRunAdmissionDecisionType.Reject => AiSharedRunStatus.Rejected,
                _ => AiSharedRunStatus.Accepted
            };
        }

        /// <summary>
        /// Creates a runtime correlation context for shared controller observability.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <returns>The runtime execution correlation context.</returns>
        private static AiRuntimeExecutionCorrelationContext CreateCorrelation(
            AiSharedRuntimeControllerRequest request)
        {
            var sharedRunId =
                request.SharedRunId ??
                request.RequestedSharedRunId;

            return new AiRuntimeExecutionCorrelationContext
            {
                CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                    ? sharedRunId ?? Guid.NewGuid().ToString("N")
                    : request.CorrelationId,

                RunId = sharedRunId
            };
        }

        /// <summary>
        /// Records a control-plane operation started event.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="operation">The operation being started.</param>
        /// <param name="correlation">The runtime correlation context.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        private async Task RecordStartedAsync(
            AiSharedRuntimeControllerRequest request,
            AiSharedRuntimeControllerOperation operation,
            AiRuntimeExecutionCorrelationContext correlation,
            CancellationToken cancellationToken)
        {
            await _observer.RecordAsync(
                new AiControlPlaneEvent
                {
                    EventType = AiControlPlaneEventType.OperationStarted,
                    Area = AiControlPlaneArea.SharedController,
                    Operation = operation.ToString(),
                    Correlation = correlation,
                    Message = $"Shared runtime controller operation '{operation}' started.",
                    Properties = new Dictionary<string, object?>
                    {
                        ["source"] = request.Source,
                        ["requestedBy"] = request.RequestedBy,
                        ["reason"] = request.Reason,
                        ["sharedRunId"] = request.SharedRunId ?? request.RequestedSharedRunId,
                        ["preferredRuntimeInstanceId"] = request.PreferredRuntimeInstanceId,
                        ["tenantId"] = request.TenantId,
                        ["pipelineKey"] = request.PipelineKey
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Records a control-plane operation completed event.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="operation">The completed operation.</param>
        /// <param name="correlation">The runtime correlation context.</param>
        /// <param name="operationResult">The internal shared controller operation result.</param>
        /// <param name="durationMs">The operation duration in milliseconds.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        private async Task RecordCompletedAsync(
            AiSharedRuntimeControllerRequest request,
            AiSharedRuntimeControllerOperation operation,
            AiRuntimeExecutionCorrelationContext correlation,
            SharedRuntimeControllerOperationResult operationResult,
            long durationMs,
            CancellationToken cancellationToken)
        {
            await _observer.RecordAsync(
                new AiControlPlaneEvent
                {
                    EventType = AiControlPlaneEventType.OperationCompleted,
                    Area = AiControlPlaneArea.SharedController,
                    Operation = operation.ToString(),
                    Outcome = AiControlPlaneOperationOutcome.Succeeded,
                    Correlation = correlation,
                    DurationMs = durationMs,
                    Message = $"Shared runtime controller operation '{operation}' completed successfully.",
                    Properties = new Dictionary<string, object?>
                    {
                        ["source"] = request.Source,
                        ["requestedBy"] = request.RequestedBy,
                        ["sharedRunId"] = operationResult.Run?.SharedRunId ?? request.SharedRunId ?? request.RequestedSharedRunId,
                        ["status"] = operationResult.Run?.Status.ToString(),
                        ["assignedRuntimeInstanceId"] = operationResult.Run?.AssignedRuntimeInstanceId,
                        ["runCount"] = operationResult.Runs.Count
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Records a control-plane operation failed event.
        /// </summary>
        /// <param name="request">The shared runtime controller request, when available.</param>
        /// <param name="operation">The failed operation.</param>
        /// <param name="correlation">The runtime correlation context.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <param name="durationMs">The operation duration in milliseconds.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        private async Task RecordFailedAsync(
            AiSharedRuntimeControllerRequest? request,
            AiSharedRuntimeControllerOperation operation,
            AiRuntimeExecutionCorrelationContext correlation,
            Exception exception,
            long durationMs,
            CancellationToken cancellationToken)
        {
            await _observer.RecordAsync(
                new AiControlPlaneEvent
                {
                    EventType = AiControlPlaneEventType.OperationFailed,
                    Area = AiControlPlaneArea.SharedController,
                    Operation = operation.ToString(),
                    Outcome = AiControlPlaneOperationOutcome.Failed,
                    Correlation = correlation,
                    DurationMs = durationMs,
                    Message = $"Shared runtime controller operation '{operation}' failed.",
                    FailureReason = exception.Message,
                    Properties = new Dictionary<string, object?>
                    {
                        ["source"] = request?.Source,
                        ["requestedBy"] = request?.RequestedBy,
                        ["sharedRunId"] = request?.SharedRunId ?? request?.RequestedSharedRunId,
                        ["exceptionType"] = exception.GetType().Name
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a shared runtime controller request for the specified operation.
        /// </summary>
        /// <param name="request">The shared runtime controller request.</param>
        /// <param name="operation">The operation being validated.</param>
        private static void ValidateRequest(
            AiSharedRuntimeControllerRequest request,
            AiSharedRuntimeControllerOperation operation)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (operation == AiSharedRuntimeControllerOperation.SubmitRun &&
                request.RunRequest is null)
            {
                throw new ArgumentException(
                    "RunRequest is required for SubmitRun operations.",
                    nameof(request));
            }

            if (RequiresSharedRunId(operation) &&
                string.IsNullOrWhiteSpace(request.SharedRunId))
            {
                throw new ArgumentException(
                    "SharedRunId is required for this shared runtime controller operation.",
                    nameof(request));
            }
        }

        /// <summary>
        /// Determines whether the operation requires a shared run identifier.
        /// </summary>
        /// <param name="operation">The shared runtime controller operation.</param>
        /// <returns><c>true</c> when the operation requires a shared run id; otherwise, <c>false</c>.</returns>
        private static bool RequiresSharedRunId(
            AiSharedRuntimeControllerOperation operation)
        {
            return operation is
                AiSharedRuntimeControllerOperation.GetRun or
                AiSharedRuntimeControllerOperation.CancelRun;
        }

        /// <summary>
        /// Ensures the requested shared runtime controller operation is enabled.
        /// </summary>
        /// <param name="operation">The shared runtime controller operation.</param>
        private void EnsureEnabled(
            AiSharedRuntimeControllerOperation operation)
        {
            var enabled = operation switch
            {
                AiSharedRuntimeControllerOperation.SubmitRun => _options.EnableSubmitRun,
                AiSharedRuntimeControllerOperation.GetRun => _options.EnableGetRun,
                AiSharedRuntimeControllerOperation.ListRuns => _options.EnableListRuns,
                AiSharedRuntimeControllerOperation.CancelRun => _options.EnableCancelRun,
                _ => false
            };

            if (!enabled)
            {
                throw new InvalidOperationException(
                    $"Shared runtime controller operation '{operation}' is disabled.");
            }
        }

        /// <summary>
        /// Calculates the control-plane operation duration in milliseconds.
        /// </summary>
        /// <param name="startedAtUtc">The operation start timestamp.</param>
        /// <param name="completedAtUtc">The operation completion timestamp.</param>
        /// <returns>
        /// The operation duration in milliseconds, or <c>0</c> when duration measurement is disabled.
        /// </returns>
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

        /// <summary>
        /// Copies shared run metadata into an immutable dictionary shape.
        /// </summary>
        /// <param name="metadata">The source metadata.</param>
        /// <returns>The copied metadata.</returns>
        private static IReadOnlyDictionary<string, string> CopyMetadata(
            IReadOnlyDictionary<string, string> metadata)
        {
            return new Dictionary<string, string>(
                metadata,
                StringComparer.Ordinal);
        }

        /// <summary>
        /// Internal operation result produced by the shared runtime controller.
        /// </summary>
        private sealed class SharedRuntimeControllerOperationResult
        {
            /// <summary>
            /// Gets the shared run record returned by single-run operations.
            /// </summary>
            public AiSharedRunRecord? Run { get; init; }

            /// <summary>
            /// Gets the shared run records returned by list operations.
            /// </summary>
            public IReadOnlyList<AiSharedRunRecord> Runs { get; init; } =
                Array.Empty<AiSharedRunRecord>();
        }
    }
}