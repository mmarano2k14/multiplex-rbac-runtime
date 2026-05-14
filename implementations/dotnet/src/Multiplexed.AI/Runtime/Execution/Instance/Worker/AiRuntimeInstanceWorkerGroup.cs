using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimeInstanceWorkerGroup"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This group coordinates multiple runtime instance workers against the same
    /// execution identifier. It starts all supplied workers, returns the first
    /// terminal execution record, and cancels the remaining workers.
    /// </para>
    /// <para>
    /// The group is an orchestration helper only. It does not perform step claiming,
    /// retry handling, throttling, or convergence itself.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeInstanceWorkerGroup : IAiRuntimeInstanceWorkerGroup
    {
        private readonly IAiRuntimeLogger _logger;
        private readonly IAiRuntimeObservability _observability;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeInstanceWorkerGroup"/> class.
        /// </summary>
        /// <param name="logger">The runtime logger.</param>
        /// <param name="observability">The runtime observability facade.</param>
        public AiRuntimeInstanceWorkerGroup(
            IAiRuntimeLogger logger,
            IAiRuntimeObservability observability)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _observability = observability ?? throw new ArgumentNullException(nameof(observability));
        }

        /// <inheritdoc />
        public async Task<AiExecutionRecord> RunExecutionAsync(
            string executionId,
            IReadOnlyCollection<IAiRuntimeInstanceWorker> workers,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(workers);

            if (workers.Count == 0)
            {
                throw new ArgumentException(
                    "At least one runtime instance worker is required.",
                    nameof(workers));
            }

            return await _observability.Tracer.TraceExecutionAsync(
                new AiExecutionTraceContext
                {
                    ExecutionId = executionId,
                    ExecutionMode = "Dag",
                    Status = "WorkerGroupRunning",
                    WorkerId = "worker-group"
                },
                async () =>
                {
                    return await RunWorkerGroupInternalAsync(
                        executionId,
                        workers,
                        cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs the supplied workers and returns the first terminal result.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="workers">The participating workers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The terminal execution record.</returns>
        private async Task<AiExecutionRecord> RunWorkerGroupInternalAsync(
            string executionId,
            IReadOnlyCollection<IAiRuntimeInstanceWorker> workers,
            CancellationToken cancellationToken)
        {
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var workerTasks = workers
                .Select(worker => RunWorkerSafelyAsync(
                    worker,
                    executionId,
                    linkedCancellation.Token))
                .ToList();

            _logger.Engine.LogInformation(
                $"[AI WORKER GROUP] Runtime instance worker group started. ExecutionId='{executionId}', WorkerCount='{workers.Count}'.");

            try
            {
                while (workerTasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(workerTasks)
                        .ConfigureAwait(false);

                    workerTasks.Remove(completedTask);

                    var result = await completedTask.ConfigureAwait(false);

                    if (result.IsTerminal)
                    {
                        _logger.Engine.LogInformation(
                            $"[AI WORKER GROUP] Terminal execution observed. ExecutionId='{executionId}', Status='{result.Status}'.");

                        await linkedCancellation.CancelAsync().ConfigureAwait(false);

                        await ObserveRemainingWorkersAsync(workerTasks)
                            .ConfigureAwait(false);

                        return result;
                    }
                }

                throw new InvalidOperationException(
                    $"Runtime instance worker group completed without observing a terminal execution. ExecutionId='{executionId}'.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.Engine.LogInformation(
                    $"[AI WORKER GROUP] Runtime instance worker group cancelled. ExecutionId='{executionId}'.");

                throw;
            }
        }

        /// <summary>
        /// Runs one worker and lets expected cancellation propagate to the group.
        /// </summary>
        /// <param name="worker">The worker to run.</param>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The execution record returned by the worker.</returns>
        private static async Task<AiExecutionRecord> RunWorkerSafelyAsync(
            IAiRuntimeInstanceWorker worker,
            string executionId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(worker);

            return await worker.RunExecutionAsync(
                executionId,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Observes remaining worker tasks after a terminal result cancels the group.
        /// </summary>
        /// <param name="workerTasks">The remaining worker tasks.</param>
        private static async Task ObserveRemainingWorkersAsync(
            IReadOnlyCollection<Task<AiExecutionRecord>> workerTasks)
        {
            foreach (var task in workerTasks)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected after the first worker observes terminal execution.
                }
            }
        }
    }
}