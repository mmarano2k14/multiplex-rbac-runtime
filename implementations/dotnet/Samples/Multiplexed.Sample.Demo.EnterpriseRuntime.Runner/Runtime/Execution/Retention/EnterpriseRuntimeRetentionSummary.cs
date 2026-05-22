using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retention
{
    /// <summary>
    /// Represents terminal retention summary information.
    /// </summary>
    public sealed class EnterpriseRuntimeRetentionSummary
    {
        /// <summary>
        /// Gets or initializes the configured maximum hot state step count.
        /// </summary>
        public int? MaxHotStateStepCount { get; init; }

        /// <summary>
        /// Gets or initializes the actual number of steps retained in hot state.
        /// </summary>
        public int ActualHotStateStepCount { get; init; }

        /// <summary>
        /// Gets or initializes the expected completed step count.
        /// </summary>
        public int? ExpectedCompletedStepCount { get; init; }

        /// <summary>
        /// Gets the number of steps no longer present in hot state.
        /// </summary>
        public int StepsRemovedFromHotState =>
            ExpectedCompletedStepCount.HasValue
                ? Math.Max(0, ExpectedCompletedStepCount.Value - ActualHotStateStepCount)
                : 0;

        /// <summary>
        /// Gets a value indicating whether the hot state limit was respected.
        /// </summary>
        public bool HotStateLimitRespected =>
            !MaxHotStateStepCount.HasValue ||
            ActualHotStateStepCount <= MaxHotStateStepCount.Value;
    }
}