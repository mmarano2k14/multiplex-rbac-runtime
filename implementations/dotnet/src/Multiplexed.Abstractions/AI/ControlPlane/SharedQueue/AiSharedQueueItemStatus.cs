namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue
{
    /// <summary>
    /// Defines the lifecycle status of a shared queue item.
    /// </summary>
    public enum AiSharedQueueItemStatus
    {
        /// <summary>
        /// The shared queue item status is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The shared queue item is pending and can be claimed.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// The shared queue item has been claimed for dispatch.
        /// </summary>
        Claimed = 2,

        /// <summary>
        /// The shared queue item was dispatched to a runtime instance.
        /// </summary>
        Dispatched = 3,

        /// <summary>
        /// The shared queue item was completed.
        /// </summary>
        Completed = 4,

        /// <summary>
        /// The shared queue item failed.
        /// </summary>
        Failed = 5,

        /// <summary>
        /// The shared queue item was cancelled.
        /// </summary>
        Cancelled = 6
    }
}