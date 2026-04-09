using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Emits structured runtime events for the AI execution engine.
    ///
    /// PURPOSE:
    /// - Centralize execution and step lifecycle observability
    /// - Emit structured realtime events through the runtime event context
    /// - Keep the execution engine focused on orchestration, not payload formatting
    ///
    /// DESIGN:
    /// - Important orchestration milestones use dedicated structured methods
    /// - Generic log methods remain available for detailed tracing and fallback diagnostics
    /// - Event categories are intentionally stable so UI timelines and sinks can rely on them
    /// </summary>
    public sealed class AiExecutionEngineLogger : IAiExecutionEngineLogger
    {
        private readonly IRuntimeEventContext _realtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionEngineLogger"/> class.
        /// </summary>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        public AiExecutionEngineLogger(IRuntimeEventContext realtime)
        {
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }

        /// <inheritdoc />
        public void ExecutionCreated(AiExecutionRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            _realtime.LogInfo(
                message: "AI execution created.",
                category: "ai.execution.created",
                data: new
                {
                    record.ExecutionId,
                    record.ContextKey,
                    record.CurrentStep,
                    record.Status,
                    record.ExecutionMode
                });
        }

        /// <inheritdoc />
        public void ExecutionLoaded(AiExecutionRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            _realtime.LogInfo(
                message: "AI execution loaded.",
                category: "ai.execution.loaded",
                data: new
                {
                    record.ExecutionId,
                    record.ContextKey,
                    record.CurrentStep,
                    record.Status,
                    record.ExecutionMode
                });
        }

        /// <inheritdoc />
        public void ExecutionCompleted(AiExecutionRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            _realtime.LogInfo(
                message: "AI execution completed.",
                category: "ai.execution.completed",
                data: new
                {
                    record.ExecutionId,
                    record.ContextKey,
                    record.CurrentStep,
                    record.Status,
                    record.ExecutionMode
                });
        }

        /// <inheritdoc />
        public void ExecutionAlreadyCompleted(AiExecutionRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            _realtime.LogInfo(
                message: "AI execution already completed.",
                category: "ai.execution.already.completed",
                data: new
                {
                    record.ExecutionId,
                    record.ContextKey,
                    record.CurrentStep,
                    record.Status,
                    record.ExecutionMode
                });
        }

        /// <inheritdoc />
        public void StepCompleted(AiExecutionRecord record, string stepName)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogInfo(
                message: $"Step '{stepName}' completed.",
                category: "ai.step.completed",
                data: new
                {
                    record.ExecutionId,
                    Step = stepName,
                    record.CurrentStep,
                    record.Status,
                    record.ExecutionMode
                });
        }

        /// <inheritdoc />
        public void StepFailed(string executionId, string stepName, string? error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogError(
                message: $"Step '{stepName}' failed.",
                category: "ai.step.failed",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Error = error
                });
        }

        /// <inheritdoc />
        public void StepException(string executionId, string stepName, Exception exception)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(exception);

            _realtime.LogError(
                message: $"Step '{stepName}' threw an exception.",
                category: "ai.step.exception",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    ExceptionType = exception.GetType().FullName,
                    Exception = exception.Message
                });
        }

        /// <inheritdoc />
        public void StepClaimed(string executionId, string stepName, string workerId, string claimToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

            _realtime.LogInfo(
                message: $"Step '{stepName}' claimed by worker '{workerId}'.",
                category: "ai.step.claimed",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    WorkerId = workerId,
                    ClaimToken = claimToken
                });
        }

        /// <inheritdoc />
        public void StepRetryScheduled(string executionId, string stepName, int retryCount, DateTime? nextRetryAtUtc)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogWarning(
                message: $"Step '{stepName}' scheduled for retry.",
                category: "ai.step.retry.scheduled",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    RetryCount = retryCount,
                    NextRetryAtUtc = nextRetryAtUtc
                });
        }

        /// <inheritdoc />
        public void StepsRecovered(string executionId, int recoveredCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogWarning(
                message: $"Recovered timed-out steps for execution '{executionId}'.",
                category: "ai.execution.steps.recovered",
                data: new
                {
                    ExecutionId = executionId,
                    RecoveredCount = recoveredCount
                });
        }

        /// <inheritdoc />
        public void ExecutionReplayRestored(string executionId, AiExecutionStatus status, int stepsCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogInfo(
                message: "AI execution restored from snapshot.",
                category: "ai.execution.replay.restored",
                data: new
                {
                    ExecutionId = executionId,
                    Status = status,
                    StepsCount = stepsCount
                });
        }

        /// <inheritdoc />
        public void ExecutionReplaySkipped(string executionId, string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            _realtime.LogInfo(
                message: "AI execution replay skipped.",
                category: "ai.execution.replay.skipped",
                data: new
                {
                    ExecutionId = executionId,
                    Reason = reason
                });
        }

        /// <inheritdoc />
        public void SnapshotPersisted(string executionId, AiExecutionStatus status)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogInfo(
                message: "AI execution snapshot persisted.",
                category: "ai.snapshot.persisted",
                data: new
                {
                    ExecutionId = executionId,
                    Status = status
                });
        }

        /// <inheritdoc />
        public void FinalizationSucceeded(string executionId, AiExecutionStatus status)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogInfo(
                message: "AI execution finalization succeeded.",
                category: "ai.execution.finalization.succeeded",
                data: new
                {
                    ExecutionId = executionId,
                    Status = status
                });
        }

        /// <inheritdoc />
        public void FinalizationRaceLost(string executionId, AiExecutionStatus status)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogWarning(
                message: "AI execution finalization race lost.",
                category: "ai.execution.finalization.race.lost",
                data: new
                {
                    ExecutionId = executionId,
                    Status = status
                });
        }

        /// <inheritdoc />
        public void CleanupStarted(string executionId, AiExecutionStatus status)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogInfo(
                message: "AI execution cleanup started.",
                category: "ai.execution.cleanup.started",
                data: new
                {
                    ExecutionId = executionId,
                    Status = status
                });
        }

        /// <inheritdoc />
        public void CleanupCompleted(string executionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogInfo(
                message: "AI execution cleanup completed.",
                category: "ai.execution.cleanup.completed",
                data: new
                {
                    ExecutionId = executionId
                });
        }

        /// <inheritdoc />
        public void CleanupSkipped(string executionId, string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            _realtime.LogInfo(
                message: "AI execution cleanup skipped.",
                category: "ai.execution.cleanup.skipped",
                data: new
                {
                    ExecutionId = executionId,
                    Reason = reason
                });
        }

        /// <inheritdoc />
        public void LogInformation(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            _realtime.LogInfo(
                message: message,
                category: "ai.execution.info",
                data: null);
        }

        /// <inheritdoc />
        public void LogWarning(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            _realtime.LogWarning(
                message: message,
                category: "ai.execution.warning",
                data: null);
        }

        /// <inheritdoc />
        public void LogError(Exception exception, string message)
        {
            ArgumentNullException.ThrowIfNull(exception);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            _realtime.LogError(
                message: message,
                category: "ai.execution.error",
                data: new
                {
                    ExceptionType = exception.GetType().FullName,
                    Exception = exception.Message
                });
        }

        /// <inheritdoc />
        public void LogError(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            _realtime.LogError(
                message: message,
                category: "ai.execution.error",
                data: null);
        }
    }
}