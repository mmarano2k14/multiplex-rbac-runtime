using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Persistence
{
    /// <summary>
    /// Loads retry execution information from persisted execution state.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionRetryLoader
    {
        /// <summary>
        /// Gets the retry count for the configured retried step.
        /// </summary>
        /// <param name="resolver">
        /// The execution step resolver.
        /// </param>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="persistedState">
        /// The persisted execution state.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The retry count.
        /// </returns>
        public async Task<int> GetRetryCountAsync(
            IAiExecutionStepResolver resolver,
            string executionId,
            AiExecutionState persistedState,
            EnterpriseRuntimeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                resolver);

            ArgumentNullException.ThrowIfNull(
                persistedState);

            ArgumentNullException.ThrowIfNull(
                request);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                executionId);

            if (string.IsNullOrWhiteSpace(
                    request.RetriedStepName))
            {
                return 0;
            }

            var retriedStep = await resolver.GetStepAsync(
                    executionId,
                    request.RetriedStepName,
                    persistedState,
                    cancellationToken)
                .ConfigureAwait(false);

            return retriedStep?.RetryState?.RetryCount ?? 0;
        }
    }
}