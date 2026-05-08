namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Defines the possible outcomes of a concurrency acquisition attempt.
    /// </summary>
    public enum AiConcurrencyDecisionKind
    {
        /// <summary>
        /// Indicates that execution capacity was acquired.
        /// </summary>
        Allowed = 0,

        /// <summary>
        /// Indicates that execution capacity is currently unavailable.
        /// </summary>
        Denied = 1
    }
}