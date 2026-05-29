namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Defines how a persisted AI execution should be replayed.
    /// </summary>
    public enum AiExecutionReplayMode
    {
        /// <summary>
        /// Loads and validates the execution without mutating state or continuing execution.
        /// </summary>
        AuditOnly = 0,

        /// <summary>
        /// Restores completed results and resumes only incomplete or orphaned work.
        /// </summary>
        ResumeIncomplete = 1,

        /// <summary>
        /// Re-executes all replayable steps. This mode is intended for tests or controlled diagnostics only.
        /// </summary>
        ReExecuteAll = 2
    }
}