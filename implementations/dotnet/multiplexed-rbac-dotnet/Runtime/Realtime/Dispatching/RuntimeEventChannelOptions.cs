namespace MultiplexedRbac.Runtime.Realtime.Dispatching
{
    /// <summary>
    /// Configuration options for the runtime event channel.
    /// </summary>
    public sealed class RuntimeEventChannelOptions
    {
        /// <summary>
        /// Maximum number of buffered runtime events.
        /// </summary>
        public int Capacity { get; set; } = 10_000;

        /// <summary>
        /// If true, writers may continue synchronously on the caller thread.
        /// </summary>
        public bool AllowSynchronousContinuations { get; set; } = false;

        /// <summary>
        /// If true, a single reader optimization is enabled.
        /// </summary>
        public bool SingleReader { get; set; } = true;

        /// <summary>
        /// If true, a single writer optimization is enabled.
        /// </summary>
        public bool SingleWriter { get; set; } = false;
    }
}
