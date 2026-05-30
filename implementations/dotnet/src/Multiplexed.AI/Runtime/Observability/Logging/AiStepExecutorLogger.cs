using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Observability.Logging
{
    /// <summary>
    /// Emits structured runtime events for single-step execution behavior.
    ///
    /// This implementation centralizes retry- and attempt-related telemetry so
    /// the step executor can remain focused on execution behavior.
    /// </summary>
    public sealed class AiStepExecutorLogger : IAiStepExecutorLogger
    {
        private readonly IRuntimeEventContext _realtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepExecutorLogger"/> class.
        /// </summary>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        public AiStepExecutorLogger(IRuntimeEventContext realtime)
        {
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }

        /// <inheritdoc />
        public void AttemptStarted(string executionId, string stepName, int attemptCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogInfo(
                message: $"Step '{stepName}' attempt {attemptCount} started.",
                category: "ai.step.attempt.start",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Attempt = attemptCount
                });
        }

        /// <inheritdoc />
        public void AttemptSucceeded(string executionId, string stepName, int attemptCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogInfo(
                message: $"Step '{stepName}' attempt {attemptCount} succeeded.",
                category: "ai.step.attempt.succeeded",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Attempt = attemptCount
                });
        }

        /// <inheritdoc />
        public void AttemptFailed(string executionId, string stepName, int attemptCount, string? error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogError(
                message: $"Step '{stepName}' attempt {attemptCount} returned a failed result.",
                category: "ai.step.attempt.failed",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Attempt = attemptCount,
                    Error = error
                });
        }

        /// <inheritdoc />
        public void AttemptException(string executionId, string stepName, int attemptCount, Exception exception)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(exception);

            _realtime.LogError(
                message: $"Step '{stepName}' attempt {attemptCount} threw an exception.",
                category: "ai.step.attempt.exception",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Attempt = attemptCount,
                    Exception = exception.Message,
                    ExceptionType = exception.GetType().FullName
                });
        }

        /// <inheritdoc />
        public void RetryScheduled(string executionId, string stepName, int attemptCount, TimeSpan delay)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogInfo(
                message: $"Step '{stepName}' retry scheduled after attempt {attemptCount}.",
                category: "ai.step.retry.scheduled",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName,
                    Attempt = attemptCount,
                    DelayMs = delay.TotalMilliseconds
                });
        }

        /// <inheritdoc />
        public void Skipped(string executionId, string stepName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            _realtime.LogInfo(
                message: $"Step '{stepName}' was skipped because it had already completed successfully.",
                category: "ai.step.skipped",
                data: new
                {
                    ExecutionId = executionId,
                    Step = stepName
                });
        }
    }
}