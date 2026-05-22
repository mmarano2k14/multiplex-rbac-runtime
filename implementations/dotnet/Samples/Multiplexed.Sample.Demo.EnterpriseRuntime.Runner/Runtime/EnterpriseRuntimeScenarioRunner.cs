namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime
{
    using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios;

    /// <summary>
    /// Coordinates enterprise runtime scenario discovery and execution.
    /// </summary>
    public sealed class EnterpriseRuntimeScenarioRunner
    {
        private readonly IReadOnlyDictionary<string, IEnterpriseRuntimeScenario> _scenarios;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnterpriseRuntimeScenarioRunner"/> class.
        /// </summary>
        /// <param name="scenarios">
        /// The available runtime scenarios.
        /// </param>
        public EnterpriseRuntimeScenarioRunner(
            IEnumerable<IEnterpriseRuntimeScenario> scenarios)
        {
            _scenarios = scenarios.ToDictionary(
                scenario => scenario.Name,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Runs the selected runtime scenario.
        /// </summary>
        /// <param name="scenarioName">
        /// The scenario name.
        /// </param>
        /// <param name="context">
        /// The scenario execution context.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The scenario execution result.
        /// </returns>
        public Task<EnterpriseRuntimeScenarioResult> RunScenarioAsync(
            string scenarioName,
            EnterpriseRuntimeScenarioContext context,
            CancellationToken cancellationToken = default)
        {
            if (!_scenarios.TryGetValue(
                    scenarioName,
                    out var scenario))
            {
                var availableScenarios = string.Join(
                    ", ",
                    _scenarios.Keys.OrderBy(x => x));

                return Task.FromResult(
                    EnterpriseRuntimeScenarioResult.Failed(
                        $"Unknown scenario '{scenarioName}'. Available scenarios: {availableScenarios}."));
            }

            return scenario.ExecuteAsync(
                context,
                cancellationToken);
        }

        /// <summary>
        /// Gets available scenario names.
        /// </summary>
        /// <returns>
        /// The available scenario names.
        /// </returns>
        public IReadOnlyCollection<string> GetScenarioNames()
        {
            return _scenarios.Keys
                .OrderBy(x => x)
                .ToArray();
        }
    }
}