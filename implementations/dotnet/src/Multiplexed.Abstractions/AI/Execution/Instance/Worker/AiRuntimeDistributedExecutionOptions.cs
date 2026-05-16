namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Defines options for distributed multi-runtime-instance execution,
    /// where multiple runtime workers cooperate on the same execution identifier.
    /// </summary>
    public sealed class AiRuntimeDistributedExecutionOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether distributed multi-runtime-instance execution is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the number of runtime workers participating in the same execution.
        /// </summary>
        public int WorkerCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether remaining workers should stop after terminal execution observation.
        /// </summary>
        public bool StopOnFirstTerminal { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum duration to wait for terminal execution observation before stopping remaining workers.
        /// </summary>
        public TimeSpan TerminalObservationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}