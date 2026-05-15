using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimeInstanceWorkerGroup"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This group coordinates multiple runtime instance workers against the same
    /// existing execution identifier. It starts all supplied workers, returns the first
    /// terminal execution record, and cancels the remaining workers.
    /// </para>
    /// <para>
    /// The group does not create executions, does not select pipelines, and must not be
    /// used to repurpose an execution identifier for another workflow run.
    /// </para>
    /// <para>
    /// Each execution run must have its own distinct execution identifier. The execution
    /// identifier is the namespace for the execution record, DAG state, step states,
    /// retention artifacts, externalized payloads, resolver indexes, snapshots, and
    /// replay data.
    /// </para>
    /// <para>
    /// Multiple runtime instance workers may safely advance the same existing execution
    /// identifier because distributed correctness is enforced by the underlying runtime
    /// engine and distributed DAG store.
    /// </para>
    /// <para>
    /// The group is an orchestration helper only. It does not perform step claiming,
    /// retry handling, throttling, retention, or convergence itself.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeInstanceWorkerGroup
    {
        /// <summary>
        /// Runs multiple runtime instance workers against the same existing execution
        /// until one worker observes a terminal execution state or cancellation is requested.
        /// </summary>
        /// <param name="executionId">
        /// The existing execution identifier to advance. This identifier must belong
        /// to a previously created execution run and must not be reused for another run.
        /// </param>
        /// <param name="workers">
        /// The runtime instance workers participating in the execution.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The terminal execution record observed by the first completing worker.
        /// </returns>
        Task<AiExecutionRecord> RunExecutionAsync(
            string executionId,
            IReadOnlyCollection<IAiRuntimeInstanceWorker> workers,
            CancellationToken cancellationToken = default);
    }
}