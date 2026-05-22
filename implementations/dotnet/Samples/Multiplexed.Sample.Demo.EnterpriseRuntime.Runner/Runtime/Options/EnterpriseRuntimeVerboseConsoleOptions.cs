namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Options
{
    /// <summary>
    /// Defines verbose console output options for the enterprise runtime demo.
    /// </summary>
    public sealed class EnterpriseRuntimeVerboseConsoleOptions
    {
        /// <summary>
        /// Gets or initializes a value indicating whether verbose console output is enabled.
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether raw realtime JSON output is enabled.
        /// </summary>
        public bool Raw { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether noisy realtime events are displayed.
        /// </summary>
        public bool Noise { get; init; }
    }
}