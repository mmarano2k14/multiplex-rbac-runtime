using Multiplexed.Abstractions.AI.Execution.Retention.Models;

namespace Multiplexed.Abstractions.AI.Execution.Retention.Triggers
{
    /// <summary>
    /// Defines a strategy used to determine whether retention should be executed
    /// for a given execution state.
    ///
    /// PURPOSE:
    /// - Avoid executing retention after every step
    /// - Trigger retention only when thresholds or conditions are met
    ///
    /// IMPORTANT:
    /// - Must be deterministic
    /// - Must be fast (no I/O)
    /// - Must NOT mutate the execution state
    /// </summary>
    public interface IAiExecutionRetentionTrigger
    {
        /// <summary>
        /// Evaluates whether retention should run for the specified context.
        /// </summary>
        /// <param name="context">The retention trigger context.</param>
        /// <returns>
        /// True if retention should be executed; otherwise false.
        /// </returns>
        bool ShouldRun(AiExecutionRetentionTriggerContext context);
    }
}