namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Throttling
{
    /// <summary>
    /// Represents throttling execution analysis results.
    /// </summary>
    public sealed class EnterpriseRuntimeThrottlingSummary
    {
        /// <summary>
        /// Gets or initializes the throttle scope.
        /// </summary>
        public required string Scope { get; init; }

        /// <summary>
        /// Gets or initializes the throttle target.
        /// </summary>
        public required string Target { get; init; }

        /// <summary>
        /// Gets or initializes the configured throttle limit.
        /// </summary>
        public int ConfiguredLimit { get; init; }

        /// <summary>
        /// Gets or initializes the observed worker count.
        /// </summary>
        public int ObservedWorkerCount { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether throttling was observed.
        /// </summary>
        public bool StepThrottlingObserved { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether the throttle limit was respected.
        /// </summary>
        public bool ThrottleRespected { get; init; }
    }
}