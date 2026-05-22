using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retry
{
    /// <summary>
    /// Analyzes retry recovery information for enterprise runtime executions.
    /// </summary>
    public sealed class EnterpriseRuntimeRetryAnalyzer
    {
        /// <summary>
        /// Creates a retry recovery summary for the execution request.
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
        /// The retry recovery summary.
        /// </returns>
        public async Task<EnterpriseRuntimeRetrySummary> AnalyzeAsync(
            IAiExecutionStepResolver resolver,
            string executionId,
            AiExecutionState persistedState,
            EnterpriseRuntimeExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                resolver);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                executionId);

            ArgumentNullException.ThrowIfNull(
                persistedState);

            ArgumentNullException.ThrowIfNull(
                request);

            var stepNames = GetExpectedRetriedStepNames(
                request);

            var retryCounts = new SortedDictionary<string, int>(
                StringComparer.Ordinal);

            foreach (var stepName in stepNames)
            {
                var step = await resolver.GetStepAsync(
                        executionId,
                        stepName,
                        persistedState,
                        cancellationToken)
                    .ConfigureAwait(false);

                retryCounts[stepName] =
                    step?.RetryState?.RetryCount ?? 0;
            }

            return new EnterpriseRuntimeRetrySummary
            {
                RetryCountsByStepName = retryCounts
            };
        }

        /// <summary>
        /// Gets the expected retried step names from the execution request.
        /// </summary>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <returns>
        /// The expected retried step names.
        /// </returns>
        private static IReadOnlyCollection<string> GetExpectedRetriedStepNames(
            EnterpriseRuntimeExecutionRequest request)
        {
            if (request.ExpectedRetriedStepNames.Count > 0)
            {
                return request.ExpectedRetriedStepNames;
            }

            if (!string.IsNullOrWhiteSpace(
                    request.RetriedStepName))
            {
                return new[]
                {
                    request.RetriedStepName
                };
            }

            return Array.Empty<string>();
        }
    }
}