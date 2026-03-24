using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Runtime.Pipeline.Steps;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Orchestrates execution of multiple AI steps in sequence.
    /// </summary>
    public sealed class AiStepRunner
    {
        private readonly IReadOnlyList<IAiStep> _steps;
        private readonly IRuntimeEventContext _realtime;

        public AiStepRunner(IEnumerable<IAiStep> steps, IRuntimeEventContext realtime)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(realtime);

            _steps = steps.ToList();
            _realtime = realtime;
        }

        /// <summary>
        /// Executes all steps sequentially using the provided context.
        /// </summary>
        public async Task<AiStepContext> RunAsync(
            AiStepContext context,
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

                _realtime.LogInfo(
                    message: $"AI step '{step.Name}' started.",
                    category: "ai.step.start",
                    data: new
                    {
                        context.ExecutionId,
                        Step = step.Name
                    });

                AiStepResult result;

                try
                {
                    // Execute step
                    result = await step.ExecuteAsync(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    _realtime.LogError(
                        message: $"AI step '{step.Name}' threw an exception.",
                        category: "ai.step.exception",
                        data: new
                        {
                            context.ExecutionId,
                            Step = step.Name,
                            DurationMs = stopwatch.ElapsedMilliseconds,
                            Exception = ex.Message
                        });

                    throw;
                }

                stopwatch.Stop();

                // Stop pipeline on failure
                if (!result.Success)
                {
                    _realtime.LogError(
                        message: $"AI step '{step.Name}' failed.",
                        category: "ai.step.failed",
                        data: new
                        {
                            context.ExecutionId,
                            Step = step.Name,
                            DurationMs = stopwatch.ElapsedMilliseconds,
                            result.Error
                        });

                    throw new InvalidOperationException(
                        $"Step '{step.Name}' failed: {result.Error ?? "Unknown error"}");
                }

                // Merge returned data into context
                foreach (var entry in result.Data)
                {
                    context.Data[entry.Key] = entry.Value;
                }

                _realtime.LogInfo(
                    message: $"AI step '{step.Name}' completed.",
                    category: "ai.step.completed",
                    data: new
                    {
                        context.ExecutionId,
                        Step = step.Name,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        result.Output
                    });
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
    }
}