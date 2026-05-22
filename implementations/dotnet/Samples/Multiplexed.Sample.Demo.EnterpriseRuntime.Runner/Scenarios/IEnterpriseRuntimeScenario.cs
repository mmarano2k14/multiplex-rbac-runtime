namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios
{
    /// <summary>
    /// Represents a runnable enterprise runtime scenario.
    /// </summary>
    public interface IEnterpriseRuntimeScenario
    {
        /// <summary>
        /// Gets the unique scenario name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Executes the scenario.
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
        Task<EnterpriseRuntimeScenarioResult> ExecuteAsync(
            EnterpriseRuntimeScenarioContext context,
            CancellationToken cancellationToken = default);
    }
}