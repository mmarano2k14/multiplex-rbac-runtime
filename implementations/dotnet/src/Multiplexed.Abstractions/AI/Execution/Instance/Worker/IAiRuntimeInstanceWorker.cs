using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Represents a runtime-instance worker capable of advancing an AI execution
    /// until it reaches a terminal state or cancellation is requested.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A runtime instance worker is a higher-level execution primitive built on top
    /// of the AI execution engine. It repeatedly advances an execution by invoking
    /// batch execution cycles and observing the latest execution state.
    /// </para>
    /// <para>
    /// When the runtime is backed by a distributed DAG store, multiple runtime
    /// instance workers may safely participate in the same execution concurrently.
    /// Distributed correctness is provided by the underlying runtime coordination
    /// layer, including atomic claims, claim tokens, retry state, lease expiration,
    /// throttling, and deterministic convergence.
    /// </para>
    /// <para>
    /// This abstraction is intended for application-level usage so callers do not
    /// need to manually loop over engine execution methods.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeInstanceWorker
    {
        /// <summary>
        /// Runs the specified execution until it reaches a terminal state or cancellation is requested.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier to advance.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token used to stop the worker loop.
        /// </param>
        /// <returns>
        /// The final execution record when the execution reaches a terminal state.
        /// </returns>
        Task<AiExecutionRecord> RunExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}