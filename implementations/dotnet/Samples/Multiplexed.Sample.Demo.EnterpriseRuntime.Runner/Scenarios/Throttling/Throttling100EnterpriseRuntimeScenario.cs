using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Throttling
{
    /// <summary>
    /// Represents the 100-step distributed throttling scenario.
    /// </summary>
    public sealed class Throttling100EnterpriseRuntimeScenario : IEnterpriseRuntimeScenario
    {
        /// <summary>
        /// Gets the scenario name.
        /// </summary>
        public string Name => EnterpriseRuntimeScenarioNames.Throttling100;

        /// <summary>
        /// Executes the scenario.
        /// </summary>
        /// <param name="context">The scenario execution context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The scenario execution result.</returns>
        public Task<EnterpriseRuntimeScenarioResult> ExecuteAsync(
            EnterpriseRuntimeScenarioContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var executionRunner = context.Services
                .GetRequiredService<EnterpriseRuntimeExecutionRunner>();

            var scenario = DistributedThrottlingScenario.Steps100();

            var request = new EnterpriseRuntimeExecutionRequest
            {
                ScenarioName = scenario.Name,
                PipelineName = scenario.PipelineName,
                PipelineInput = EnterpriseRuntimePipelineInput.FromDefinition(
                    scenario.PipelineDefinition),
                Input = new
                {
                    candidateId = scenario.CandidateId,
                    source = scenario.Name,
                    stepCount = scenario.StepCount,
                    workerCount = scenario.WorkerCount,
                    provider = scenario.Provider,
                    model = scenario.Model,
                    operationName = scenario.Operation,
                    providerThrottleLimit = scenario.ProviderThrottleLimit,
                    throttling = true
                },
                ExpectedCompletedStepCount = scenario.StepCount,
                MinimumWorkerCount = scenario.ProviderThrottleLimit,
                CleanupExecutionBundle = true,
                Timeout = scenario.Timeout,
                ValidateReplay = false
            };

            return executionRunner.RunAsync(
                request,
                cancellationToken);
        }
    }
}