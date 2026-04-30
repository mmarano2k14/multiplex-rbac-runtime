using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Observability
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimeObservability"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Aggregates Metrics, Tracing, and Logging into a single runtime facade.
    /// - Used by execution engines and runtime services.
    ///
    /// DESIGN:
    /// - Pure composition (no logic).
    /// - Safe for high-performance execution.
    /// </remarks>
    public sealed class AiRuntimeObservability : IAiRuntimeObservability
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeObservability"/> class.
        /// </summary>
        public AiRuntimeObservability(
            IAiRuntimeMetrics metrics,
            IAiRuntimeTracer tracer,
            IAiRuntimeLogger logger)
        {
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            Tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IAiRuntimeMetrics Metrics { get; }

        /// <inheritdoc />
        public IAiRuntimeTracer Tracer { get; }

        /// <inheritdoc />
        public IAiRuntimeLogger Logger { get; }
    }
}