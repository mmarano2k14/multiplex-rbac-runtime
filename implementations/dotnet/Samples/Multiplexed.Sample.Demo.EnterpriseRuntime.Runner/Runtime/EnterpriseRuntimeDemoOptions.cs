namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime
{
    /// <summary>
    /// Represents console runner options.
    /// </summary>
    public sealed class EnterpriseRuntimeDemoOptions
    {
        /// <summary>
        /// Gets or sets the scenario name.
        /// </summary>
        public string Scenario { get; set; } = "json";

        /// <summary>
        /// Gets or sets a value indicating whether infrastructure should be started automatically.
        /// </summary>
        public bool StartInfrastructure { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether verbose runtime output is enabled.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether raw realtime JSON output is enabled.
        /// </summary>
        public bool VerboseRaw { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether noisy realtime events are displayed.
        /// </summary>
        public bool VerboseNoise { get; set; }
    }
}