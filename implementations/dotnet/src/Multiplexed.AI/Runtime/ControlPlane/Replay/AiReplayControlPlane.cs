using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Observability.Tracing;

namespace Multiplexed.AI.Runtime.ControlPlane.Replay
{
    /// <summary>
    /// Runtime implementation of the replay control-plane facade.
    ///
    /// This class wraps the existing replay engine and exposes adapter-neutral
    /// replay operations for future HTTP API, MCP, CLI, dashboard, and
    /// Kubernetes control-plane adapters.
    ///
    /// Important:
    /// This class must not re-run LLM calls, tool calls, providers,
    /// workflow steps, or DAG execution logic.
    /// </summary>
    /// <remarks>
    /// Replay control-plane operations are side-effect safe by default.
    ///
    /// The control-plane facade intentionally maps Replay, Audit, Report,
    /// Ledger, and Timeline operations to AuditOnly replay mode because the
    /// replay API must not re-run external providers, tools, LLM calls,
    /// or side-effecting workflow logic.
    ///
    /// ReExecuteAll is intentionally not exposed by this control-plane facade in V1.
    /// </remarks>
    public sealed class AiReplayControlPlane : IAiReplayControlPlane
    {
        private readonly IAiExecutionReplayService _replayService;
        private readonly AiReplayControlOptions _options;

        public AiReplayControlPlane(
            IAiExecutionReplayService replayService,
            IOptions<AiReplayControlOptions> options)
        {
            _replayService = replayService ?? throw new ArgumentNullException(nameof(replayService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<AiReplayControlResult> ExecuteAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request.Operation switch
            {
                AiReplayOperation.Replay => ReplayAsync(request, cancellationToken),
                AiReplayOperation.Audit => AuditAsync(request, cancellationToken),
                AiReplayOperation.Restore => RestoreAsync(request, cancellationToken),
                AiReplayOperation.GetReport => GetReportAsync(request, cancellationToken),
                AiReplayOperation.GetLedger => GetLedgerAsync(request, cancellationToken),
                AiReplayOperation.GetTimeline => GetTimelineAsync(request, cancellationToken),

                _ => throw new NotSupportedException(
                    $"Replay control-plane operation '{request.Operation}' is not supported.")
            };
        }

        public Task<AiReplayControlResult> ReplayAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteReplayOperationAsync(
                request,
                AiReplayOperation.Replay,
                cancellationToken);
        }

        public Task<AiReplayControlResult> AuditAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteReplayOperationAsync(
                request,
                AiReplayOperation.Audit,
                cancellationToken);
        }

        public Task<AiReplayControlResult> RestoreAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteReplayOperationAsync(
                request,
                AiReplayOperation.Restore,
                cancellationToken);
        }

        public Task<AiReplayControlResult> GetReportAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteReplayOperationAsync(
                request,
                AiReplayOperation.GetReport,
                cancellationToken);
        }

        public Task<AiReplayControlResult> GetLedgerAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteReplayOperationAsync(
                request,
                AiReplayOperation.GetLedger,
                cancellationToken);
        }

        public Task<AiReplayControlResult> GetTimelineAsync(
            AiReplayControlRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteReplayOperationAsync(
                request,
                AiReplayOperation.GetTimeline,
                cancellationToken);
        }

        private async Task<AiReplayControlResult> ExecuteReplayOperationAsync(
            AiReplayControlRequest request,
            AiReplayOperation operation,
            CancellationToken cancellationToken)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;

            try
            {
                ValidateRequest(request);
                EnsureEnabled(operation);

                var replayRequest = new AiExecutionReplayRequest
                {
                    ExecutionId = request.ExecutionId,
                    Mode = MapReplayMode(operation),
                    IncludeTimeline = request.IncludeTimeline,
                    IncludeLedgerEvents = request.IncludeLedger,
                    IncludeStepDetails = request.IncludeReport || request.IncludeDiagnostics,
                    ValidatePayloadReferences = true
                };

                var report = await _replayService
                    .ReplayAsync(replayRequest, cancellationToken)
                    .ConfigureAwait(false);

                var completedAtUtc = DateTimeOffset.UtcNow;
                var success = !request.StrictDeterminism || report.ReplayValid;

                return new AiReplayControlResult
                {
                    ExecutionId = request.ExecutionId,
                    Operation = operation,
                    Success = success,
                    Message = BuildResultMessage(operation, success, report.ReplayValid),
                    Report = request.IncludeReport ? report : null,
                    Ledger = request.IncludeLedger
                        ? report.LedgerEvents
                        : Array.Empty<AiDecisionLedgerEntry>(),
                    Timeline = request.IncludeTimeline
                        ? report.TimelineEvents
                        : Array.Empty<AiTraceEvent>(),
                    Diagnostics = request.IncludeDiagnostics
                        ? BuildDiagnostics(report)
                        : Array.Empty<string>(),
                    Deterministic = report.ReplayValid,
                    CorrelationId = request.CorrelationId,
                    RuntimeInstanceId = request.RuntimeInstanceId,
                    RequestedBy = request.RequestedBy,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = CalculateDurationMs(startedAtUtc, completedAtUtc),
                    FailureReason = success ? null : report.FailureReason
                };
            }
            catch (Exception exception) when (_options.ReturnFailureResultInsteadOfThrowing)
            {
                var completedAtUtc = DateTimeOffset.UtcNow;

                return new AiReplayControlResult
                {
                    ExecutionId = request?.ExecutionId ?? string.Empty,
                    Operation = operation,
                    Success = false,
                    Message = $"Replay control-plane operation '{operation}' failed.",
                    Diagnostics = request?.IncludeDiagnostics == true
                        ? new[] { exception.Message }
                        : Array.Empty<string>(),
                    CorrelationId = request?.CorrelationId,
                    RuntimeInstanceId = request?.RuntimeInstanceId,
                    RequestedBy = request?.RequestedBy,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = CalculateDurationMs(startedAtUtc, completedAtUtc),
                    FailureReason = exception.Message
                };
            }
        }

        private static AiExecutionReplayMode MapReplayMode(AiReplayOperation operation)
        {
            return operation switch
            {
                AiReplayOperation.Replay => AiExecutionReplayMode.AuditOnly,
                AiReplayOperation.Audit => AiExecutionReplayMode.AuditOnly,
                AiReplayOperation.GetReport => AiExecutionReplayMode.AuditOnly,
                AiReplayOperation.GetLedger => AiExecutionReplayMode.AuditOnly,
                AiReplayOperation.GetTimeline => AiExecutionReplayMode.AuditOnly,

                AiReplayOperation.Restore => AiExecutionReplayMode.ResumeIncomplete,

                _ => throw new NotSupportedException(
                    $"Replay control-plane operation '{operation}' is not supported.")
            };
        }

        private static void ValidateRequest(AiReplayControlRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.ExecutionId))
            {
                throw new ArgumentException(
                    "ExecutionId is required for replay control-plane operations.",
                    nameof(request));
            }
        }

        private void EnsureEnabled(AiReplayOperation operation)
        {
            var enabled = operation switch
            {
                AiReplayOperation.Replay => _options.EnableReplay,
                AiReplayOperation.Audit => _options.EnableAudit,
                AiReplayOperation.Restore => _options.EnableRestore,
                AiReplayOperation.GetReport => _options.EnableReportAccess,
                AiReplayOperation.GetLedger => _options.EnableLedgerAccess,
                AiReplayOperation.GetTimeline => _options.EnableTimelineAccess,
                _ => false
            };

            if (!enabled)
            {
                throw new InvalidOperationException(
                    $"Replay control-plane operation '{operation}' is disabled.");
            }
        }

        private static string BuildResultMessage(
            AiReplayOperation operation,
            bool success,
            bool replayValid)
        {
            if (success && replayValid)
            {
                return $"Replay control-plane operation '{operation}' completed successfully.";
            }

            if (success)
            {
                return $"Replay control-plane operation '{operation}' completed with non-strict replay validation issues.";
            }

            return $"Replay control-plane operation '{operation}' completed with replay validation issues.";
        }

        private static IReadOnlyList<string> BuildDiagnostics(
            AiExecutionReplayReport report)
        {
            if (report.Issues.Count == 0)
            {
                return Array.Empty<string>();
            }

            return report.Issues
                .Select(issue => issue.Message)
                .ToArray();
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
    }
}