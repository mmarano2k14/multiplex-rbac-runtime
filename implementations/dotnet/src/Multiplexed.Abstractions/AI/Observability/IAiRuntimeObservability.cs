using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Tracing;

namespace Multiplexed.Abstractions.AI.Observability
{
    /// <summary>
    /// Central observability facade for the AI runtime.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Provides a unified entry point for Metrics, Tracing, and Logging.
    /// - Simplifies instrumentation across the runtime.
    ///
    /// IMPORTANT:
    /// - The runtime must depend on this interface only.
    /// - No direct dependency on Metrics, Tracer, or Logger individually.
    /// </remarks>
    public interface IAiRuntimeObservability
    {
        /// <summary>
        /// Gets the runtime metrics component.
        /// </summary>
        IAiRuntimeMetrics Metrics { get; }

        /// <summary>
        /// Gets the runtime tracer component.
        /// </summary>
        IAiRuntimeTracer Tracer { get; }

        /// <summary>
        /// Gets the runtime logger component.
        /// </summary>
        //IAiRuntimeLogger Logger { get; }
    }
}