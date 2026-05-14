using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Coordinates multiple runtime instance workers participating in the same AI execution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A runtime instance worker group is a higher-level orchestration primitive that
    /// starts multiple runtime instance workers against the same execution identifier.
    /// </para>
    /// <para>
    /// The group does not implement distributed correctness itself. Distributed safety
    /// remains provided by the runtime engine, Redis-backed DAG store, atomic claims,
    /// claim tokens, retry state, concurrency leases, throttling, and deterministic
    /// convergence.
    /// </para>
    /// <para>
    /// This abstraction is intended for application and test usage so callers do not
    /// need to manually coordinate multiple worker tasks.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeInstanceWorkerGroup
    {
        /// <summary>
        /// Runs multiple runtime instance workers against the same execution until one
        /// worker observes a terminal execution state or cancellation is requested.
        /// </summary>
        /// <param name="executionId">The execution identifier to advance.</param>
        /// <param name="workers">The runtime instance workers participating in the execution.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The terminal execution record observed by the first completing worker.</returns>
        Task<AiExecutionRecord> RunExecutionAsync(
            string executionId,
            IReadOnlyCollection<IAiRuntimeInstanceWorker> workers,
            CancellationToken cancellationToken = default);
    }
}