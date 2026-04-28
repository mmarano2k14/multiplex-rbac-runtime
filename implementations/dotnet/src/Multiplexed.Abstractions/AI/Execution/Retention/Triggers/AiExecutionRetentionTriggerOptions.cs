namespace Multiplexed.Abstractions.AI.Execution.Retention.Triggers
{
    /// <summary>
    /// Defines threshold options used by the default execution retention trigger.
    ///
    /// PURPOSE:
    /// - Configure when retention should be triggered.
    /// - Keep threshold-based retention decisions outside the execution engine.
    /// </summary>
    public sealed class AiExecutionRetentionTriggerOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of steps allowed in the execution state before retention is triggered.
        /// </summary>
        public int MaxStepsInState { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum number of completed steps allowed in the execution state before retention is triggered.
        /// </summary>
        public int MaxCompletedStepsInState { get; set; } = 500;

        /// <summary>
        /// Gets or sets the maximum estimated inline payload size, in bytes, before retention is triggered.
        /// </summary>
        public long MaxInlinePayloadBytes { get; set; } = 5 * 1024 * 1024;
    }
}