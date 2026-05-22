namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios
{
    /// <summary>
    /// Represents the result of a runtime scenario execution.
    /// </summary>
    public sealed class EnterpriseRuntimeScenarioResult
    {
        /// <summary>
        /// Gets or initializes a value indicating whether the scenario completed successfully.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets or initializes the optional result message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Gets or initializes the scenario duration.
        /// </summary>
        public TimeSpan Duration { get; init; }

        /// <summary>
        /// Creates a completed scenario result.
        /// </summary>
        /// <param name="duration">
        /// The scenario duration.
        /// </param>
        /// <param name="message">
        /// The optional result message.
        /// </param>
        /// <returns>
        /// The completed scenario result.
        /// </returns>
        public static EnterpriseRuntimeScenarioResult Completed(
            TimeSpan duration,
            string? message = null)
        {
            return new EnterpriseRuntimeScenarioResult
            {
                Success = true,
                Duration = duration,
                Message = message
            };
        }

        /// <summary>
        /// Creates a failed scenario result.
        /// </summary>
        /// <param name="message">
        /// The failure message.
        /// </param>
        /// <returns>
        /// The failed scenario result.
        /// </returns>
        public static EnterpriseRuntimeScenarioResult Failed(
            string message)
        {
            return new EnterpriseRuntimeScenarioResult
            {
                Success = false,
                Message = message
            };
        }
    }
}