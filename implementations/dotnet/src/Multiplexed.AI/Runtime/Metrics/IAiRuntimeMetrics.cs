using Multiplexed.AI.Runtime.Metrics.Execution;
using Multiplexed.AI.Runtime.Metrics.HotState;
using Multiplexed.AI.Runtime.Metrics.Resolvers;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Multiplexed.AI.Runtime.Metrics.Storage;

namespace Multiplexed.AI.Runtime.Metrics
{
    /// <summary>
    /// Central facade for all AI runtime metrics.
    ///
    /// PURPOSE:
    /// - Provide a single entry point for runtime observability.
    /// - Expose specialized metric domains through strongly typed properties.
    /// - Keep execution, retention, storage, hot-state, and resolver metrics separated.
    ///
    /// DESIGN:
    /// - This interface does not expose flat counters.
    /// - Each domain owns its own metrics contract.
    /// - Runtime services should use the relevant domain property.
    ///
    /// IMPORTANT:
    /// - Metrics are observational only.
    /// - Metrics must never drive business logic, execution decisions, or state transitions.
    /// </summary>
    public interface IAiRuntimeMetrics
    {
        /// <summary>
        /// Gets metrics related to AI execution lifecycle.
        /// </summary>
        IAiExecutionMetrics Execution { get; }

        /// <summary>
        /// Gets metrics related to retention, compaction, eviction, and archival.
        /// </summary>
        IAiRetentionMetrics Retention { get; }

        /// <summary>
        /// Gets metrics related to payload and persistence storage.
        /// </summary>
        IAiStorageMetrics Storage { get; }

        /// <summary>
        /// Gets metrics related to hot execution state size and lifecycle.
        /// </summary>
        IAiHotStateMetrics HotState { get; }

        /// <summary>
        /// Gets metrics related to runtime resolvers and input binding resolution.
        /// </summary>
        IAiResolverMetrics Resolver { get; }
    }
}