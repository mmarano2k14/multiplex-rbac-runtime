namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Represents the strategy used to compute retry delays between attempts.
    /// </summary>
    public enum AiRetryBackoffStrategy
    {
        /// <summary>
        /// Uses the same delay for every retry attempt.
        /// </summary>
        Fixed = 0,

        /// <summary>
        /// Increases the retry delay linearly based on the retry attempt count.
        /// </summary>
        Linear = 1,

        /// <summary>
        /// Increases the retry delay exponentially based on the retry attempt count.
        /// </summary>
        Exponential = 2
    }
}