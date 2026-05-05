namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Defines threshold settings used to decide whether retention should run.
    /// </summary>
    /// <remarks>
    /// Missing values fall back to conservative defaults.
    /// </remarks>
    public sealed class AiRetentionTriggerDefinition
    {
        /// <summary>
        /// Gets a value indicating whether trigger evaluation is enabled.
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        /// Gets the maximum number of steps allowed in hot execution state before retention runs.
        /// </summary>
        public int MaxStepsInState { get; init; } = 1000;

        /// <summary>
        /// Gets the maximum number of completed steps allowed in hot execution state before retention runs.
        /// </summary>
        public int MaxCompletedStepsInState { get; init; } = 500;

        /// <summary>
        /// Gets the maximum estimated inline payload size allowed before retention runs.
        /// </summary>
        public long MaxInlinePayloadBytes { get; init; } = 5 * 1024 * 1024;
    }
}