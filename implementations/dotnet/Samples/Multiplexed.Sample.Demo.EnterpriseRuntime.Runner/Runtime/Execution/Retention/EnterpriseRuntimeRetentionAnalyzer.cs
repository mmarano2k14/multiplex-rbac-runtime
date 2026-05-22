using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retention
{
    /// <summary>
    /// Analyzes terminal retention information for enterprise runtime executions.
    /// </summary>
    public sealed class EnterpriseRuntimeRetentionAnalyzer
    {
        /// <summary>
        /// Creates a terminal retention summary.
        /// </summary>
        /// <param name="state">
        /// The persisted execution state.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <returns>
        /// The retention summary.
        /// </returns>
        public EnterpriseRuntimeRetentionSummary Analyze(
            AiExecutionState state,
            EnterpriseRuntimeExecutionRequest request)
        {
            ArgumentNullException.ThrowIfNull(
                state);

            ArgumentNullException.ThrowIfNull(
                request);

            return new EnterpriseRuntimeRetentionSummary
            {
                MaxHotStateStepCount = request.MaxHotStateStepCount,
                ActualHotStateStepCount = state.Steps.Count,
                ExpectedCompletedStepCount = request.ExpectedCompletedStepCount
            };
        }
    }
}