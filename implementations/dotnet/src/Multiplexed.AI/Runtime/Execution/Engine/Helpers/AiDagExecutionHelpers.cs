using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Engine.Helpers
{
    /// <summary>
    /// Provides small shared helper methods for DAG execution.
    /// </summary>
    internal static class AiDagExecutionHelpers
    {
        /// <summary>
        /// Gets the required optimistic execution step key.
        /// </summary>
        /// <param name="record">The execution record.</param>
        /// <returns>The execution step key.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the execution step key is missing.
        /// </exception>
        public static string GetRequiredExecutionStepKey(
            AiExecutionRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (string.IsNullOrWhiteSpace(record.ExecutionStepKey))
            {
                throw new InvalidOperationException(
                    "ExecutionStepKey must be set before persisting execution state.");
            }

            return record.ExecutionStepKey;
        }

        /// <summary>
        /// Ensures that a distributed DAG store is configured.
        /// </summary>
        /// <param name="dagStore">The DAG store instance.</param>
        /// <typeparam name="TDagStore">The DAG store type.</typeparam>
        /// <returns>The configured DAG store.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the DAG store is not configured.
        /// </exception>
        public static TDagStore RequireDagStore<TDagStore>(
            TDagStore? dagStore)
            where TDagStore : class
        {
            return dagStore
                ?? throw new InvalidOperationException(
                    "Distributed DAG store is not configured.");
        }

        /// <summary>
        /// Determines whether an execution can be finalized from the current hot state.
        /// </summary>
        /// <param name="state">The execution state.</param>
        /// <param name="targetStatus">The target terminal status.</param>
        /// <returns>
        /// <c>true</c> when the execution can be finalized; otherwise, <c>false</c>.
        /// </returns>
        [Obsolete(
            "CanFinalize is obsolete. Finalization is now driven by convergence evaluation (AiDagExecutionConvergenceEvaluator). " +
            "This method is not archive-aware and should not be used in retention-enabled execution paths.",
            false)]
        public static bool CanFinalize(
            AiExecutionState state,
            AiExecutionStatus targetStatus)
        {
            ArgumentNullException.ThrowIfNull(state);

            var steps = state.Steps.Values.ToList();

            if (steps.Count == 0)
            {
                return false;
            }

            if (steps.Any(x =>
                x.Status == AiStepExecutionStatus.Running ||
                x.Status == AiStepExecutionStatus.WaitingForRetry ||
                x.Status == AiStepExecutionStatus.Ready ||
                x.Status == AiStepExecutionStatus.None))
            {
                return false;
            }

            if (targetStatus == AiExecutionStatus.Completed)
            {
                return steps.All(x => x.IsCompleted);
            }

            if (targetStatus == AiExecutionStatus.Failed)
            {
                return steps.Any(x => x.Status == AiStepExecutionStatus.Failed)
                    && steps.All(x =>
                        x.Status == AiStepExecutionStatus.Failed ||
                        x.Status == AiStepExecutionStatus.Completed);
            }

            if (targetStatus == AiExecutionStatus.Cancelled)
            {
                return steps.All(x =>
                    x.Status == AiStepExecutionStatus.Completed ||
                    x.Status == AiStepExecutionStatus.Failed);
            }

            return false;
        }
    }
}