using Multiplexed.Abstractions.AI.Execution.Scheduling;
using System.Collections.Concurrent;

namespace Multiplexed.AI.Runtime.Execution.Scheduling
{
    /// <summary>
    /// Default implementation of <see cref="IAiDagStepExecutionOrchestrator"/>.
    /// </summary>
    /// <remarks>
    /// This implementation is the central execution gateway for claimed DAG steps.
    /// It supports bounded parallel execution while remaining ready for future
    /// distributed concurrency control, policy-based execution admission,
    /// metrics, and tracing.
    /// </remarks>
    public sealed class DefaultAiDagStepExecutionOrchestrator
        : IAiDagStepExecutionOrchestrator
    {
        /// <inheritdoc />
        public async Task<AiStepExecutionBatchResult> ExecuteAsync(
            AiDagStepExecutionContext context,
            AiStepExecutionBatch batch,
            AiStepExecutionDelegate executeStepAsync,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(executeStepAsync);

            if (batch.IsEmpty)
            {
                return new AiStepExecutionBatchResult
                {
                    Results = Array.Empty<AiClaimedStepExecutionResult>()
                };
            }

            var results = new ConcurrentBag<AiClaimedStepExecutionResult>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, context.MaxDegreeOfParallelism),
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(
                batch.Steps,
                parallelOptions,
                async (claimedStep, ct) =>
                {
                    var result = await executeStepAsync(
                        claimedStep,
                        ct);

                    results.Add(new AiClaimedStepExecutionResult
                    {
                        ClaimedStep = claimedStep,
                        Result = result
                    });
                });

            return new AiStepExecutionBatchResult
            {
                Results = results.ToArray()
            };
        }
    }
}