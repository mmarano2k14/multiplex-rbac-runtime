using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Observability
{
    /// <summary>
    /// Default implementation of the AI runtime observability facade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This facade exposes the runtime observability services used by the deterministic AI runtime,
    /// including metrics, tracing, logging, decision ledger recording, and ambient runtime correlation.
    /// </para>
    ///
    /// <para>
    /// The facade does not own execution state. It only groups observability services behind a
    /// single runtime-facing abstraction.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeObservability : IAiRuntimeObservability
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeObservability"/> class.
        /// </summary>
        /// <param name="metrics">The runtime metrics recorder.</param>
        /// <param name="tracer">The runtime tracer.</param>
        /// <param name="logger">The runtime logger.</param>
        /// <param name="ledger">The decision ledger recorder.</param>
        /// <param name="correlation">The ambient runtime correlation accessor.</param>
        public AiRuntimeObservability(
            IAiRuntimeMetrics metrics,
            IAiRuntimeTracer tracer,
            IAiRuntimeLogger logger,
            IAiDecisionLedgerRecorder ledger,
            IAiRuntimeCorrelationAccessor correlation)
        {
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            Tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
        }

        /// <inheritdoc />
        public IAiRuntimeMetrics Metrics { get; }

        /// <inheritdoc />
        public IAiRuntimeTracer Tracer { get; }

        /// <inheritdoc />
        public IAiRuntimeLogger Logger { get; }

        /// <inheritdoc />
        public IAiDecisionLedgerRecorder Ledger { get; }

        /// <inheritdoc />
        public IAiRuntimeCorrelationAccessor Correlation { get; }
    }
}