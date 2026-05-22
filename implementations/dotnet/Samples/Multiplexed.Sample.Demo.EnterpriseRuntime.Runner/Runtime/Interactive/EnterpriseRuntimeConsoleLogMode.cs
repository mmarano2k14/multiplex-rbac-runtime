namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Interactive
{
    /// <summary>
    /// Defines interactive console log modes for the enterprise runtime demo.
    /// </summary>
    public enum EnterpriseRuntimeConsoleLogMode
    {
        /// <summary>
        /// Disables verbose realtime console output.
        /// </summary>
        None = 0,

        /// <summary>
        /// Enables readable verbose realtime console output.
        /// </summary>
        Verbose = 1,

        /// <summary>
        /// Enables readable verbose realtime console output and raw JSON event output.
        /// </summary>
        VerboseRaw = 2,

        /// <summary>
        /// Enables readable verbose realtime console output including noisy internal events.
        /// </summary>
        VerboseNoise = 3
    }
}