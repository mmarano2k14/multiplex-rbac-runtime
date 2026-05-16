using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Creates runtime instance workers for distributed execution scenarios.
    /// </summary>
    public interface IAiRuntimeInstanceWorkerFactory
    {
        /// <summary>
        /// Creates runtime instance workers for the same execution.
        /// </summary>
        /// <param name="workerCount">The number of runtime instance workers to create.</param>
        /// <returns>The created runtime instance workers.</returns>
        IReadOnlyCollection<IAiRuntimeInstanceWorker> CreateWorkers(
            int workerCount);
    }
}