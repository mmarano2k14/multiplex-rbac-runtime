namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retry
{
    /// <summary>
    /// Represents retry recovery summary information for an enterprise runtime execution.
    /// </summary>
    public sealed class EnterpriseRuntimeRetrySummary
    {
        /// <summary>
        /// Gets or initializes the retry counts by step name.
        /// </summary>
        public required IReadOnlyDictionary<string, int> RetryCountsByStepName { get; init; }

        /// <summary>
        /// Gets the number of retried steps.
        /// </summary>
        public int RetriedStepCount =>
            RetryCountsByStepName.Count(item => item.Value > 0);

        /// <summary>
        /// Gets the minimum retry count across expected retried steps.
        /// </summary>
        public int MinimumRetryCount =>
            RetryCountsByStepName.Count == 0
                ? 0
                : RetryCountsByStepName.Values.Min();

        /// <summary>
        /// Gets the maximum retry count across expected retried steps.
        /// </summary>
        public int MaximumRetryCount =>
            RetryCountsByStepName.Count == 0
                ? 0
                : RetryCountsByStepName.Values.Max();

        /// <summary>
        /// Gets a value indicating whether all expected retried steps retried at least once.
        /// </summary>
        public bool AllExpectedStepsRetried =>
            RetryCountsByStepName.Count > 0 &&
            RetryCountsByStepName.Values.All(count => count > 0);
    }
}