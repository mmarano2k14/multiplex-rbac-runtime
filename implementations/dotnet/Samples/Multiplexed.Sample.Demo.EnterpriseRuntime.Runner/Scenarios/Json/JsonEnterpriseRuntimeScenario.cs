using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Json
{
    /// <summary>
    /// Represents the JSON enterprise runtime scenario.
    /// </summary>
    public sealed class JsonEnterpriseRuntimeScenario: IEnterpriseRuntimeScenario
    {
        /// <summary>
        /// Gets the scenario name.
        /// </summary>
        public string Name => EnterpriseRuntimeScenarioNames.Json;

        /// <summary>
        /// Executes the JSON runtime scenario.
        /// </summary>
        /// <param name="context">
        /// The scenario execution context.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The scenario execution result.
        /// </returns>
        public Task<EnterpriseRuntimeScenarioResult> ExecuteAsync(
            EnterpriseRuntimeScenarioContext context,
            CancellationToken cancellationToken = default)
        {
            var executionRunner = context.Services
                .GetRequiredService<EnterpriseRuntimeExecutionRunner>();

            var pipelineFilePath = EnterpriseRuntimeDemoPaths.GetPipelineFilePath();

            var request = EnterpriseRuntimeExecutionRequestFactory.CreateJsonDemo(
                Name,
                pipelineFilePath);

            return executionRunner.RunAsync(
                request,
                cancellationToken);
        }
    }
}