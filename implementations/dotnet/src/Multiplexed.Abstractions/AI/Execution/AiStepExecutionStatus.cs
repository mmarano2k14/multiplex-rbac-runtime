namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the execution lifecycle state of a single pipeline step.
    ///
    /// This status is the source of truth for DAG execution.
    /// It replaces the need for a global "current step" concept.
    /// </summary>
    public enum AiStepExecutionStatus
    {
        /// <summary>
        /// The step has not been initialized yet.
        /// </summary>
        None = 0,

        /// <summary>
        /// The step is ready to be executed but has not started yet.
        /// </summary>
        Ready = 1,

        /// <summary>
        /// The step is currently executing.
        /// </summary>
        Running = 2,

        /// <summary>
        /// The step has successfully completed.
        /// </summary>
        Completed = 3,

        /// <summary>
        /// The step has failed terminally and will not be retried again.
        /// </summary>
        Failed = 4,

        /// <summary>
        /// The step has failed, but a retry is still allowed.
        ///
        /// While in this state, the step must not be executed again until
        /// its retry window becomes due.
        /// </summary>
        WaitingForRetry = 5,
    }
}