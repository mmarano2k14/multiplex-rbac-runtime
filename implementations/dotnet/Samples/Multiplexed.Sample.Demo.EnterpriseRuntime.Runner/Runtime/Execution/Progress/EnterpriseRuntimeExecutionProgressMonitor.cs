using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.AI.Stores;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Progress
{
    /// <summary>
    /// Monitors enterprise runtime execution progress while the execution is running.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionProgressMonitor
    {
        /// <summary>
        /// Monitors execution progress until cancellation is requested.
        /// </summary>
        /// <param name="handle">
        /// The run handle.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <param name="dagStore">
        /// The DAG execution store.
        /// </param>
        /// <param name="metrics">
        /// The runtime metrics facade.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task MonitorAsync(
            AiRuntimeWorkerRunHandle handle,
            EnterpriseRuntimeExecutionRequest request,
            IAiDagExecutionStore dagStore,
            IAiRuntimeMetrics metrics,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                handle);

            ArgumentNullException.ThrowIfNull(
                request);

            ArgumentNullException.ThrowIfNull(
                dagStore);

            ArgumentNullException.ThrowIfNull(
                metrics);

            string? lastLine = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(
                        handle.ExecutionId))
                {
                    await Task.Delay(
                            TimeSpan.FromMilliseconds(500),
                            cancellationToken)
                        .ConfigureAwait(false);

                    continue;
                }

                var snapshot = await CreateSnapshotAsync(
                        handle.ExecutionId,
                        request,
                        dagStore,
                        metrics)
                    .ConfigureAwait(false);

                var line = snapshot.Format();

                if (!string.Equals(
                        line,
                        lastLine,
                        StringComparison.Ordinal))
                {
                    WriteProgressLine(
                        line,
                        lastLine);

                    lastLine = line;
                }

                await Task.Delay(
                        TimeSpan.FromMilliseconds(750),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates an execution progress snapshot.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <param name="dagStore">
        /// The DAG execution store.
        /// </param>
        /// <param name="metrics">
        /// The runtime metrics facade.
        /// </param>
        /// <returns>
        /// The progress snapshot.
        /// </returns>
        private static async Task<EnterpriseRuntimeExecutionProgressSnapshot> CreateSnapshotAsync(
            string executionId,
            EnterpriseRuntimeExecutionRequest request,
            IAiDagExecutionStore dagStore,
            IAiRuntimeMetrics metrics)
        {
            var record = await dagStore.GetRecordAsync(
                    executionId)
                .ConfigureAwait(false);

            var state = await dagStore.GetStateAsync(
                    executionId)
                .ConfigureAwait(false);

            var retryCount = state?.Steps.Values
                .Sum(step => step.RetryState?.RetryCount ?? 0) ?? 0;

            var workerCycles = metrics.Worker.GetCyclesByRuntimeInstance();

            return new EnterpriseRuntimeExecutionProgressSnapshot
            {
                ExecutionId = executionId,
                CompletedSteps = record?.CompletedSteps.Count ?? 0,
                ExpectedCompletedSteps = request.ExpectedCompletedStepCount,
                RetryCount = retryCount,
                WorkerCount = workerCycles.Count,
                HotStateStepCount = state?.Steps.Count ?? 0,
                MaxHotStateStepCount = request.MaxHotStateStepCount
            };
        }

        /// <summary>
        /// Writes a progress line by updating the current console line.
        /// </summary>
        /// <param name="line">
        /// The progress line.
        /// </param>
        /// <param name="previousLine">
        /// The previous progress line.
        /// </param>
        private static void WriteProgressLine(
            string line,
            string? previousLine)
        {
            var clearLength = Math.Max(
                previousLine?.Length ?? 0,
                line.Length);

            Console.Write(
                "\r" + line.PadRight(clearLength));
        }
    }
}