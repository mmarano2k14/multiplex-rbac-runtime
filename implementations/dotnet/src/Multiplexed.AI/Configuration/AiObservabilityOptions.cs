using Multiplexed.Abstractions.AI.Metrics.Store;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.AI.Observability.Ledger;
using Multiplexed.AI.Runtime.Tracing;

namespace Multiplexed.AI.Configuration
{
    /// <summary>
    /// Configuration options controlling AI runtime observability behavior.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Centralizes all observability-related toggles for tracing, metrics, recording, and decision ledger audit.
    /// - Allows switching implementations without modifying the runtime engine.
    ///
    /// DESIGN:
    /// - These options are consumed at DI composition time.
    /// - They must remain simple and side-effect free.
    ///
    /// IMPORTANT:
    /// - Disabling tracing, metrics, or ledger recording must not impact runtime correctness.
    /// - Observability is always optional and must remain non-blocking unless strict ledger recording is explicitly enabled.
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

        /// <summary>
        /// Gets or sets runtime metric persistence options.
        /// </summary>
        /// <remarks>
        /// PURPOSE:
        /// - Controls whether runtime metric records are disabled, stored in memory,
        ///   persisted to MongoDB, or written to both memory and MongoDB.
        /// - Allows live in-memory diagnostics while also supporting durable metric history.
        ///
        /// IMPORTANT:
        /// - Metric persistence must remain best-effort and must not break runtime execution.
        /// - The in-memory mode is useful for tests, demos, and live local dashboards.
        /// - The MongoDB mode is useful for durable observability, replay preparation, and audit queries.
        /// </remarks>
        public AiRuntimeMetricStoreOptions Metrics { get; set; } = new()
        {
            Mode = AiRuntimeMetricStoreMode.Memory
        };

        /// <summary>
        /// Gets or sets decision ledger recorder options.
        /// </summary>
        /// <remarks>
        /// PURPOSE:
        /// - Controls whether decision ledger writes are disabled, best-effort, or strict.
        /// - Selects the ledger storage mode such as none, in-memory, or MongoDB.
        ///
        /// IMPORTANT:
        /// - Best-effort ledger failures must not break execution.
        /// - Strict ledger failures may fail or block execution when audit durability is mandatory.
        /// </remarks>
        public AiDecisionLedgerRecorderOptions DecisionLedger { get; set; } = new()
        {
            WriteMode = AiDecisionLedgerWriteMode.BestEffort,
            StorageMode = AiDecisionLedgerStorageMode.None
        };

        /// <summary>
        /// Gets or sets MongoDB-backed decision ledger storage options.
        /// </summary>
        /// <remarks>
        /// These options are only used when <see cref="DecisionLedger"/> is configured
        /// with <see cref="AiDecisionLedgerStorageMode.Mongo"/>.
        /// </remarks>
        public MongoAiDecisionLedgerOptions MongoDecisionLedger { get; set; } = new();
    }
}