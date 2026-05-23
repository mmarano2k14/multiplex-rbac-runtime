namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Control
{
    /// <summary>
    /// Represents the interactive execution control state.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionControlState
    {
        /// <summary>
        /// Gets or sets a value indicating whether the execution is currently paused by the console.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether cancel has been requested by the console.
        /// </summary>
        public bool IsCancelRequested { get; set; }

        /// <summary>
        /// Gets or sets the local runner cancellation source used to unblock the console runner.
        /// </summary>
        public CancellationTokenSource? RunnerCancellationSource { get; set; }

        /// <summary>
        /// Gets or sets RealtimeOutputSuspended, which indicates whether the console should suspend displaying realtime output.
        /// </summary>
        public bool RealtimeOutputSuspended { get; set; } = false;
    }
}