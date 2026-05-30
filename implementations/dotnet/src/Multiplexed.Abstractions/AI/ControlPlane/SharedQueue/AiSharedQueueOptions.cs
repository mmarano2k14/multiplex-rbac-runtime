namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue
{
    /// <summary>
    /// Defines policy options for the shared queue abstraction.
    /// </summary>
    public sealed class AiSharedQueueOptions
    {
        /// <summary>
        /// Enables shared queue enqueue operations.
        /// </summary>
        public bool EnableEnqueue { get; init; } = true;

        /// <summary>
        /// Enables shared queue claim operations.
        /// </summary>
        public bool EnableClaim { get; init; } = true;

        /// <summary>
        /// Enables shared queue complete operations.
        /// </summary>
        public bool EnableComplete { get; init; } = true;

        /// <summary>
        /// Enables shared queue requeue operations.
        /// </summary>
        public bool EnableRequeue { get; init; } = true;

        /// <summary>
        /// Enables shared queue cancel operations.
        /// </summary>
        public bool EnableCancel { get; init; } = true;

        /// <summary>
        /// Default claim lease duration.
        /// </summary>
        public TimeSpan DefaultClaimTtl { get; init; } = TimeSpan.FromSeconds(30);
    }
}