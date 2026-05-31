using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Dispatch;

namespace Multiplexed.Abstractions.AI.ControlPlane.SharedQueue.Pump
{
    /// <summary>
    /// Represents the result of one shared queue pump cycle.
    /// </summary>
    public sealed class AiSharedQueuePumpResult
    {
        /// <summary>
        /// Indicates whether the pump cycle completed without an unexpected failure.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Runtime instance id used for this pump cycle.
        /// </summary>
        public required string RuntimeInstanceId { get; init; }

        /// <summary>
        /// Number of dispatch attempts executed during this cycle.
        /// </summary>
        public int AttemptedDispatchCount { get; init; }

        /// <summary>
        /// Number of successful dispatches during this cycle.
        /// </summary>
        public int SuccessfulDispatchCount { get; init; }

        /// <summary>
        /// Number of failed dispatches during this cycle.
        /// </summary>
        public int FailedDispatchCount { get; init; }

        /// <summary>
        /// Indicates whether the cycle stopped because no pending item was available.
        /// </summary>
        public bool StoppedBecauseNoItemAvailable { get; init; }

        /// <summary>
        /// Optional failure reason.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// UTC timestamp when the pump cycle started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; init; }

        /// <summary>
        /// UTC timestamp when the pump cycle completed.
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; init; }

        /// <summary>
        /// Pump cycle duration in milliseconds.
        /// </summary>
        public long DurationMs { get; init; }

        /// <summary>
        /// Individual dispatch results produced during the cycle.
        /// </summary>
        public IReadOnlyList<AiSharedQueueDispatchResult> DispatchResults { get; init; } =
            Array.Empty<AiSharedQueueDispatchResult>();

        /// <summary>
        /// Optional diagnostics.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; init; } =
            Array.Empty<string>();
    }
}