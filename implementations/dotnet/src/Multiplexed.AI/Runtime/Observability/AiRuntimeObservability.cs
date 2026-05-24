using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Observability
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimeObservability"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Aggregates Metrics, Tracing, Logging, and Decision Ledger recording into a single runtime facade.
    /// - Used by execution engines and runtime services.
    ///
    /// DESIGN:
    /// - Pure composition.
    /// - No runtime decision logic.
    /// - Safe for high-performance execution.
    /// - Decision ledger recording remains controlled by the configured recorder write mode.
    /// </remarks>
    public sealed class AiRuntimeObservability : IAiRuntimeObservability
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeObservability"/> class.
        /// </summary>
        /// <param name="metrics">The runtime metrics facade.</param>
        /// <param name="tracer">The runtime tracer.</param>
        /// <param name="logger">The runtime logger.</param>
        /// <param name="ledger">The decision ledger recorder.</param>
        public AiRuntimeObservability(
            IAiRuntimeMetrics metrics,
            IAiRuntimeTracer tracer,
            IAiRuntimeLogger logger,
            IAiDecisionLedgerRecorder ledger)
        {
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            Tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        }

        /// <inheritdoc />
        public IAiRuntimeMetrics Metrics { get; }

        /// <inheritdoc />
        public IAiRuntimeTracer Tracer { get; }

        /// <inheritdoc />
        public IAiRuntimeLogger Logger { get; }

        /// <inheritdoc />
        public IAiDecisionLedgerRecorder Ledger { get; }
    }
}