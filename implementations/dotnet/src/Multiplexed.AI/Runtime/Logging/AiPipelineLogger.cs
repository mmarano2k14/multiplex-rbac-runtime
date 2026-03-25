using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Emits structured runtime events for sequential pipeline execution.
    ///
    /// This implementation centralizes pipeline-level telemetry so the step runner
    /// can focus on step orchestration rather than event formatting.
    /// </summary>
    public sealed class AiPipelineLogger : IAiPipelineLogger
    {
        private readonly IRuntimeEventContext _realtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPipelineLogger"/> class.
        /// </summary>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        public AiPipelineLogger(IRuntimeEventContext realtime)
        {
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }

        /// <inheritdoc />
        public void ExecutionStarted(string executionId, int stepCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogInfo(
                message: "AI pipeline execution started.",
                category: "ai.pipeline.start",
                data: new
                {
                    ExecutionId = executionId,
                    StepCount = stepCount
                });
        }

        /// <inheritdoc />
        public void ExecutionCompleted(string executionId, int stepCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _realtime.LogInfo(
                message: "AI pipeline execution completed.",
                category: "ai.pipeline.completed",
                data: new
                {
                    ExecutionId = executionId,
                    StepCount = stepCount
                });
        }

        /// <inheritdoc />
        public void StepStarted(AiExecutionContext context, IAiStep step)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(step);

            _realtime.LogInfo(
                message: $"AI step '{step.Name}' started.",
                category: "ai.step.start",
                data: new
                {
                    context.ExecutionId,
                    Step = step.Name
                });
        }

        /// <inheritdoc />
        public void StepException(AiExecutionContext context, IAiStep step, long durationMs, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(exception);

            _realtime.LogError(
                message: $"AI step '{step.Name}' threw an exception.",
                category: "ai.step.exception",
                data: new
                {
                    context.ExecutionId,
                    Step = step.Name,
                    DurationMs = durationMs,
                    Exception = exception.Message
                });
        }

        /// <inheritdoc />
        public void StepFailed(AiExecutionContext context, IAiStep step, long durationMs, AiStepResult result)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(result);

            _realtime.LogError(
                message: $"AI step '{step.Name}' failed.",
                category: "ai.step.failed",
                data: new
                {
                    context.ExecutionId,
                    Step = step.Name,
                    DurationMs = durationMs,
                    result.Error
                });
        }

        /// <inheritdoc />
        public void StepCompleted(AiExecutionContext context, IAiStep step, long durationMs, AiStepResult result)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(result);

            _realtime.LogInfo(
                message: $"AI step '{step.Name}' completed.",
                category: "ai.step.completed",
                data: new
                {
                    context.ExecutionId,
                    Step = step.Name,
                    DurationMs = durationMs,
                    result.Output
                });
        }
    }
}