using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Throttling
{
    /// <summary>
    /// Analyzes distributed throttling execution results.
    /// </summary>
    public sealed class EnterpriseRuntimeThrottlingAnalyzer
    {
        /// <summary>
        /// Analyzes throttling execution results.
        /// </summary>
        /// <param name="state">
        /// The persisted execution state.
        /// </param>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <param name="observedWorkerCount">
        /// The observed distributed worker count.
        /// </param>
        /// <returns>
        /// The throttling summary.
        /// </returns>
        public EnterpriseRuntimeThrottlingSummary Analyze(
            AiExecutionState state,
            EnterpriseRuntimeExecutionRequest request,
            int observedWorkerCount)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(request);

            if (!request.ScenarioName.Contains(
                    "throttling",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new EnterpriseRuntimeThrottlingSummary
                {
                    Scope = "n/a",
                    Target = "n/a",
                    ConfiguredLimit = 0,
                    ObservedWorkerCount = observedWorkerCount,
                    StepThrottlingObserved = false,
                    ThrottleRespected = true
                };
            }

            var configuredLimit = 3;

            return new EnterpriseRuntimeThrottlingSummary
            {
                Scope = "provider",
                Target = "openai",
                ConfiguredLimit = configuredLimit,
                ObservedWorkerCount = observedWorkerCount,
                StepThrottlingObserved = observedWorkerCount <= configuredLimit,
                ThrottleRespected = observedWorkerCount <= configuredLimit
            };
        }
    }
}