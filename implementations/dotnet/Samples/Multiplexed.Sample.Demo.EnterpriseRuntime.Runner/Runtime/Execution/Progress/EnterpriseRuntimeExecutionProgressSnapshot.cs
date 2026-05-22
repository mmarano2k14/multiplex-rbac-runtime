namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Progress
{
    /// <summary>
    /// Represents a point-in-time execution progress snapshot.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionProgressSnapshot
    {
        /// <summary>
        /// Gets or initializes the execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets or initializes the number of completed steps.
        /// </summary>
        public int CompletedSteps { get; init; }

        /// <summary>
        /// Gets or initializes the expected completed step count.
        /// </summary>
        public int? ExpectedCompletedSteps { get; init; }

        /// <summary>
        /// Gets or initializes the total retry count.
        /// </summary>
        public int RetryCount { get; init; }

        /// <summary>
        /// Gets or initializes the number of active distributed workers.
        /// </summary>
        public int WorkerCount { get; init; }

        /// <summary>
        /// Gets or initializes the number of steps currently retained in hot state.
        /// </summary>
        public int HotStateStepCount { get; init; }

        /// <summary>
        /// Gets or initializes the maximum configured hot state step count.
        /// </summary>
        public int? MaxHotStateStepCount { get; init; }

        /// <summary>
        /// Formats the progress snapshot as a console line.
        /// </summary>
        /// <returns>
        /// The formatted progress line.
        /// </returns>
        public string Format()
        {
            var expected = ExpectedCompletedSteps.HasValue
                ? ExpectedCompletedSteps.Value.ToString()
                : "?";

            var maxHotState = MaxHotStateStepCount.HasValue
                ? MaxHotStateStepCount.Value.ToString()
                : "?";

            return
                $"Progress: {CompletedSteps}/{expected} completed | retries={RetryCount} | workers={WorkerCount} | hotStateSteps={HotStateStepCount}/{maxHotState}";
        }
    }
}