namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Defines structured runtime logging for single-step execution behavior.
    ///
    /// This logger is responsible for retry-oriented and attempt-oriented events such as:
    /// - attempt start
    /// - attempt success
    /// - attempt failure
    /// - thrown exception
    /// - retry scheduling
    /// - idempotent skip
    /// </summary>
    public interface IAiStepExecutorLogger
    {
        /// <summary>
        /// Emits a structured event when a step attempt starts.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        /// <param name="attemptCount">The current attempt number.</param>
        void AttemptStarted(string executionId, string stepName, int attemptCount);

        /// <summary>
        /// Emits a structured event when a step attempt succeeds.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        /// <param name="attemptCount">The current attempt number.</param>
        void AttemptSucceeded(string executionId, string stepName, int attemptCount);

        /// <summary>
        /// Emits a structured event when a step attempt returns a failed result.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        /// <param name="attemptCount">The current attempt number.</param>
        /// <param name="error">The returned error message.</param>
        void AttemptFailed(string executionId, string stepName, int attemptCount, string? error);

        /// <summary>
        /// Emits a structured event when a step attempt throws an exception.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        /// <param name="attemptCount">The current attempt number.</param>
        /// <param name="exception">The thrown exception.</param>
        void AttemptException(string executionId, string stepName, int attemptCount, Exception exception);

        /// <summary>
        /// Emits a structured event when a retry is scheduled.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        /// <param name="attemptCount">The current attempt number.</param>
        /// <param name="delay">The delay before the next retry attempt.</param>
        void RetryScheduled(string executionId, string stepName, int attemptCount, TimeSpan delay);

        /// <summary>
        /// Emits a structured event when a completed step is skipped.
        /// </summary>
        /// <param name="executionId">The current execution identifier.</param>
        /// <param name="stepName">The logical step name.</param>
        void Skipped(string executionId, string stepName);
    }
}