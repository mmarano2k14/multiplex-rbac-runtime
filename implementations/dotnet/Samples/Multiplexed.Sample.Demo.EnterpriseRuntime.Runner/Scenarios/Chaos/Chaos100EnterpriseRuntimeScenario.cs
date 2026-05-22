using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos
{
    /// <summary>
    /// Represents the 100-step distributed chaos scenario.
    /// </summary>
    public sealed class Chaos100EnterpriseRuntimeScenario
        : IEnterpriseRuntimeScenario
    {
        /// <summary>
        /// Gets the scenario name.
        /// </summary>
        public string Name => EnterpriseRuntimeScenarioNames.Chaos100;

        /// <inheritdoc />
        public Task<EnterpriseRuntimeScenarioResult> ExecuteAsync(
            EnterpriseRuntimeScenarioContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                context);

            var executionRunner = context.Services
                .GetRequiredService<EnterpriseRuntimeExecutionRunner>();

            var scenario = DistributedChaosScenario.Steps100();

            var request = EnterpriseRuntimeExecutionRequestFactory.CreateChaosDemo(
                scenario);

            return executionRunner.RunAsync(
                request,
                cancellationToken);
        }
    }
}