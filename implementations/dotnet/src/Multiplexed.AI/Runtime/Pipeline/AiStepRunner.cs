using System.Diagnostics;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Orchestrates sequential execution of AI steps using a shared execution context.
    ///
    /// Responsibilities:
    /// - Execute registered steps in order
    /// - Emit structured runtime events for observability
    /// - Merge successful step outputs into the shared execution state
    /// - Stop execution on the first failure
    /// </summary>
    public sealed class AiStepRunner
    {
        private readonly IReadOnlyList<IAiStep> _steps;
        private readonly IRuntimeEventContext _realtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepRunner"/> class.
        /// </summary>
        /// <param name="steps">The ordered list of steps to execute.</param>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        public AiStepRunner(IEnumerable<IAiStep> steps, IRuntimeEventContext realtime)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(realtime);

            _steps = steps.ToList();
            _realtime = realtime;
        }

        /// <summary>
        /// Executes all registered steps sequentially using the provided execution context.
        /// </summary>
        /// <param name="context">The shared execution context.</param>
        /// <param name="cancellationToken">The cancellation token for the active execution.</param>
        /// <returns>The same execution context after pipeline completion.</returns>
        public async Task<AiExecutionContext> RunAsync(
            AiExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            _realtime.LogInfo(
                message: "AI pipeline execution started.",
                category: "ai.pipeline.start",
                data: new
                {
                    context.ExecutionId,
                    StepCount = _steps.Count
                });

            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stopwatch = Stopwatch.StartNew();

                LogStepStarted(context, step);

                AiStepResult result;

                try
                {
                    result = await step.ExecuteAsync(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    LogStepException(context, step, stopwatch.ElapsedMilliseconds, ex);
                    throw;
                }

                stopwatch.Stop();

                if (!result.Success)
                {
                    LogStepFailure(context, step, stopwatch.ElapsedMilliseconds, result);

                    throw new InvalidOperationException(
                        $"Step '{step.Name}' failed: {result.Error ?? "Unknown error"}");
                }

                MergeResult(context, result);

                LogStepCompleted(context, step, stopwatch.ElapsedMilliseconds, result);
            }

            _realtime.LogInfo(
                message: "AI pipeline execution completed.",
                category: "ai.pipeline.completed",
                data: new
                {
                    context.ExecutionId,
                    StepCount = _steps.Count
                });

            return context;
        }

        /// <summary>
        /// Merges successful step output into the shared execution state.
        /// </summary>
        /// <param name="context">The shared execution context.</param>
        /// <param name="result">The successful step result.</param>
        private static void MergeResult(AiExecutionContext context, AiStepResult result)
        {
            if (result.Data is null || result.Data.Count == 0)
                return;

            foreach (var entry in result.Data)
            {
                context.Set(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Emits a structured log event when a step starts.
        /// </summary>
        private void LogStepStarted(AiExecutionContext context, IAiStep step)
        {
            _realtime.LogInfo(
                message: $"AI step '{step.Name}' started.",
                category: "ai.step.start",
                data: new
                {
                    context.ExecutionId,
                    Step = step.Name
                });
        }

        /// <summary>
        /// Emits a structured log event when a step throws an exception.
        /// </summary>
        private void LogStepException(
            AiExecutionContext context,
            IAiStep step,
            long durationMs,
            Exception exception)
        {
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

        /// <summary>
        /// Emits a structured log event when a step returns a failed result.
        /// </summary>
        private void LogStepFailure(
            AiExecutionContext context,
            IAiStep step,
            long durationMs,
            AiStepResult result)
        {
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

        /// <summary>
        /// Emits a structured log event when a step completes successfully.
        /// </summary>
        private void LogStepCompleted(
            AiExecutionContext context,
            IAiStep step,
            long durationMs,
            AiStepResult result)
        {
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