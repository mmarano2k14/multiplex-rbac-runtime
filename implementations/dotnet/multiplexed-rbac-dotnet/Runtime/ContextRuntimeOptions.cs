namespace MultiplexedRbac.Runtime
{
    public sealed class ContextRuntimeOptions
    {
        /// <summary>
        /// Header used to transmit the Access Context handle.
        /// The same header is reused in the response if rotation occurs.
        /// </summary>
        public string AccessContextHeader { get; set; } = "X-Access-Context";

        /// <summary>
        /// Logical session idle timeout.
        /// Used to determine when a session should expire due to inactivity.
        /// This does not replace Redis TTL, but works alongside it.
        /// </summary>
        public TimeSpan SessionIdleTimeout { get; set; }
            = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Enables or disables automatic key rotation at the end of a request.
        /// Not in use in the example
        /// </summary>
        public bool EnableRotation { get; set; } = true;

        /// <summary>
        /// Rotate only when response status code is below this threshold.
        /// Not in use in the example
        /// Default: rotate only on successful responses (< 400).
        /// </summary>
        public int RotateWhenStatusCodeBelow { get; set; } = 400;
    }
}
