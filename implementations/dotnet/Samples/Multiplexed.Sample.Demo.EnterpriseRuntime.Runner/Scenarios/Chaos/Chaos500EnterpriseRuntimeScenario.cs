using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos
{
    /// <summary>
    /// Represents the 500-step distributed chaos scenario.
    /// </summary>
    public sealed class Chaos500EnterpriseRuntimeScenario
        : IEnterpriseRuntimeScenario
    {
        /// <summary>
        /// Gets the scenario name.
        /// </summary>
        public string Name => EnterpriseRuntimeScenarioNames.Chaos500;

        /// <inheritdoc />
        public Task<EnterpriseRuntimeScenarioResult> ExecuteAsync(
            EnterpriseRuntimeScenarioContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                context);

            var executionRunner = context.Services
                .GetRequiredService<EnterpriseRuntimeExecutionRunner>();

            var scenario = DistributedChaosScenario.Steps500();

            var request = EnterpriseRuntimeExecutionRequestFactory.CreateChaosDemo(
                scenario);

            return executionRunner.RunAsync(
                request,
                cancellationToken);
        }
    }
}