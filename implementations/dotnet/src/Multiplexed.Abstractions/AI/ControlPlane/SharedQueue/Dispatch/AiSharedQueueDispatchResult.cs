using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Dispatch;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Store;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Queue;

namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Dispatch
{
    /// <summary>
    /// Represents the result of claiming and dispatching one shared queue item.
    /// </summary>
    public sealed class AiSharedQueueDispatchResult
    {
        /// <summary>
        /// Indicates whether a queue item was claimed and dispatched successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Indicates whether no pending queue item was available.
        /// </summary>
        public bool NoItemAvailable { get; init; }

        /// <summary>
        /// Shared run id that was claimed, when available.
        /// </summary>
        public string? SharedRunId { get; init; }

        /// <summary>
        /// Runtime instance id that attempted the dispatch.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Queue item claimed for dispatch.
        /// </summary>
        public AiSharedQueueItem? QueueItem { get; init; }

        /// <summary>
        /// Shared run record loaded from the shared run store.
        /// </summary>
        public AiSharedRunRecord? SharedRun { get; init; }

        /// <summary>
        /// Dispatch result produced by the shared run dispatcher.
        /// </summary>
        public AiSharedRunDispatchResult? DispatchResult { get; init; }

        /// <summary>
        /// Optional status message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Optional failure reason.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// UTC timestamp when the operation started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; init; }

        /// <summary>
        /// UTC timestamp when the operation completed.
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; init; }

        /// <summary>
        /// Operation duration in milliseconds.
        /// </summary>
        public long DurationMs { get; init; }

        /// <summary>
        /// Optional diagnostics.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; init; } =
            Array.Empty<string>();
    }
}