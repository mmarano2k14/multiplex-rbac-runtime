namespace Multiplexed.Abstractions.AI.ControlPlane.SharedController.Scaling
{
    /// <summary>
    /// Represents the result of publishing or handling a runtime scale-out request.
    /// </summary>
    public sealed class AiRuntimeScaleOutRequestResult
    {
        /// <summary>
        /// Indicates whether the scale-out request was accepted by the publisher.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Shared run id that triggered the scale-out request.
        /// </summary>
        public required string SharedRunId { get; init; }

        /// <summary>
        /// Optional scale-out request id produced by the publisher.
        /// </summary>
        public string? ScaleOutRequestId { get; init; }

        /// <summary>
        /// Optional requested target instance count.
        /// </summary>
        public int? RequestedTargetInstanceCount { get; init; }

        /// <summary>
        /// Optional status message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Optional failure reason.
        /// </summary>
        public string? FailureReason { get; init; }

        /// <summary>
        /// UTC timestamp when the request was published.
        /// </summary>
        public DateTimeOffset PublishedAtUtc { get; init; }

        /// <summary>
        /// Optional diagnostics.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; init; } =
            Array.Empty<string>();
    }
}