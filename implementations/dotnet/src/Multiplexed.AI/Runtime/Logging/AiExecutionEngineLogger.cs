using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Emits structured runtime events for the AI execution engine.
    ///
    /// This implementation centralizes event categories and payload formats
    /// so the execution engine can remain focused on orchestration behavior.
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