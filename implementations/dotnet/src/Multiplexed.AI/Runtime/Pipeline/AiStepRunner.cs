using System.Diagnostics;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Runtime.Logging;

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
        private readonly IAiRuntimeLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepRunner"/> class.
        /// </summary>
        /// <param name="steps">The ordered list of steps to execute.</param>
        /// <param name="logger">The centralized AI runtime logger responsible for structured tracing across engine, pipeline, and step execution.</param>
        public AiStepRunner(IEnumerable<IAiStep> steps, IAiRuntimeLogger logger)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(logger);

            _steps = steps.ToList();
            _logger = logger;
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

            _logger.Pipeline.ExecutionStarted(context.ExecutionId, _steps.Count);

            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stopwatch = Stopwatch.StartNew();

                _logger.Pipeline.StepStarted(context, step);

                AiStepResult result;

                try
                {
                    result = await step.ExecuteAsync(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.Pipeline.StepException(context, step, stopwatch.ElapsedMilliseconds, ex);
                    throw;
                }

                stopwatch.Stop();

                if (!result.Success)
                {
                    _logger.Pipeline.StepFailed(context, step, stopwatch.ElapsedMilliseconds, result);
                    throw new InvalidOperationException(
                        $"Step '{step.Name}' failed: {result.Error ?? "Unknown error"}");
                }

                MergeResult(context, result);

                _logger.Pipeline.StepCompleted(context, step, stopwatch.ElapsedMilliseconds, result);

            }

            _logger.Pipeline.ExecutionCompleted(context.ExecutionId, _steps.Count);

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
    }
}