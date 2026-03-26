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
                    record.CurrentStep
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

            _realtime.LogInfo(
                message: $"Step '{stepName}' completed.",
                category: "ai.step.completed",
                data: new
                {
                    record.ExecutionId,
                    record.CurrentStep,
                    record.Status
                });
        }

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
                    record.CurrentStep
                });
        }

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
                    record.CurrentStep
                });
        }

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
                    record.CurrentStep
                });
        }
    }
}