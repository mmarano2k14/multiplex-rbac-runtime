namespace Multiplexed.Rbac.Core.Policies
{
    /// <summary>
    /// Defines the strategy applied when the in-flight limit is exceeded.
    /// </summary>
    public enum InFlightOverflowPolicy
    {
        /// <summary>
        /// Reject the request immediately.
        /// This is the only policy currently implemented.
        /// </summary>
        Reject,

        /// <summary>
        /// Reserved for future implementation.
        /// Requests could wait until a slot becomes available.
        /// </summary>
        Wait,

        /// <summary>
        /// Reserved for future implementation.
        /// Requests could be queued in Redis or another distributed queue.
        /// </summary>
        Queue
    }
}
