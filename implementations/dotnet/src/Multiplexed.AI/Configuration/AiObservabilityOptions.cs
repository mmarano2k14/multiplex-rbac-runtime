namespace Multiplexed.AI.Configuration
{
    /// <summary>
    /// Configuration options controlling AI runtime observability behavior.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Centralizes all observability-related toggles (tracing, metrics, recording).
    /// - Allows switching implementations without modifying the runtime engine.
    ///
    /// DESIGN:
    /// - These options are consumed at DI composition time.
    /// - They must remain simple and side-effect free.
    ///
    /// IMPORTANT:
    /// - Disabling tracing must not impact runtime correctness.
    /// - Observability is always optional and must remain non-blocking.
    /// </remarks>
    public sealed class AiObservabilityOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// </summary>
        /// <remarks>
        /// When disabled:
        /// - <see cref="NoOpAiRuntimeTracer"/> is used.
        /// - No trace scopes are created.
        /// </remarks>
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether in-memory trace recording is enabled.
        /// </summary>
        /// <remarks>
        /// When disabled:
        /// - Trace events are not stored in memory.
        /// - Useful for high-throughput production scenarios.
        /// </remarks>
        public bool EnableInMemoryRecording { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether metrics collection is enabled.
        /// </summary>
        /// <remarks>
        /// When disabled:
        /// - Metric recording calls should be ignored.
        /// </remarks>
        public bool EnableMetrics { get; set; } = true;
    }
}