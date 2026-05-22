using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retention
{
    /// <summary>
    /// Validates retained hot execution state after terminal completion.
    /// </summary>
    public static class EnterpriseRuntimeRetentionValidator
    {
        /// <summary>
        /// Validates that the persisted hot state does not exceed the configured maximum.
        /// </summary>
        /// <param name="state">
        /// The persisted execution state.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
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

            var actualHotStateSteps = state.Steps.Count;
            var expectedMaxHotStateSteps = request.MaxHotStateStepCount.Value;

            if (actualHotStateSteps > expectedMaxHotStateSteps)
            {
                throw new InvalidOperationException(
                    $"Expected terminal hot state to contain at most '{expectedMaxHotStateSteps}' steps, but found '{actualHotStateSteps}'.");
            }
        }
    }
}