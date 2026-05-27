using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retention
{
    /// <summary>
    /// Validates retained hot execution state after terminal completion.
    /// </summary>
    public static class EnterpriseRuntimeRetentionValidator
    {
        /// <summary>
        /// Validates that the retained terminal hot state does not exceed the configured maximum.
        /// </summary>
        /// <param name="state">
        /// The persisted execution state.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <remarks>
        /// Evicted steps may still remain in <see cref="AiExecutionState.Steps"/> as lightweight
        /// reconstruction shells. Those shells are intentionally kept so the resolver can rebuild
        /// archived step data from payload storage.
        ///
        /// Therefore, this validator must not use <c>state.Steps.Count</c> directly. It must count
        /// only steps that are still physically retained in hot state.
        /// </remarks>
        public static void ValidateHotStateLimit(
            AiExecutionState state,
            EnterpriseRuntimeExecutionRequest request)
        {
            ArgumentNullException.ThrowIfNull(
                state);

            ArgumentNullException.ThrowIfNull(
                request);

            if (!request.MaxHotStateStepCount.HasValue)
            {
                return;
            }

            var actualHotStateSteps = state.Steps.Values.Count(
                step => !step.IsEvictedFromHotState);

            var evictedShellSteps = state.Steps.Values.Count(
                step => step.IsEvictedFromHotState);

            var totalTrackedSteps = state.Steps.Count;
            var expectedMaxHotStateSteps = request.MaxHotStateStepCount.Value;

            if (actualHotStateSteps > expectedMaxHotStateSteps)
            {
                throw new InvalidOperationException(
                    $"Expected terminal hot state to contain at most '{expectedMaxHotStateSteps}' retained hot steps, " +
                    $"but found '{actualHotStateSteps}'. TotalTrackedSteps='{totalTrackedSteps}', EvictedShellSteps='{evictedShellSteps}'.");
            }
        }
    }
}