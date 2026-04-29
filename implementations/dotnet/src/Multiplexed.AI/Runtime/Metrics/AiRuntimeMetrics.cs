using System;
using Multiplexed.AI.Runtime.Metrics.Execution;
using Multiplexed.AI.Runtime.Metrics.HotState;
using Multiplexed.AI.Runtime.Metrics.Resolvers;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Multiplexed.AI.Runtime.Metrics.Storage;

namespace Multiplexed.AI.Runtime.Metrics
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimeMetrics"/>.
    ///
    /// PURPOSE:
    /// - Acts as the central facade for all AI runtime metrics.
    /// - Exposes specialized metric domains through strongly typed properties.
    /// - Keeps runtime observability structured and maintainable.
    ///
    /// DESIGN:
    /// - This class does not record metrics directly.
    /// - This class does not own counters or aggregation logic.
    /// - Domain-specific metrics belong in their dedicated implementations.
    ///
    /// IMPORTANT:
    /// - Keep this class lightweight.
    /// - Do not reintroduce execution, retention, storage, hot-state, or resolver counters here.
    /// </summary>
    public sealed class AiRuntimeMetrics : IAiRuntimeMetrics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeMetrics"/> class.
        /// </summary>
        /// <param name="execution">Execution lifecycle metrics.</param>
        /// <param name="retention">Retention lifecycle metrics.</param>
        /// <param name="storage">Storage and payload metrics.</param>
        /// <param name="hotState">Hot execution state metrics.</param>
        /// <param name="resolver">Resolver and input binding metrics.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when one of the required metric domains is <c>null</c>.
        /// </exception>
        public AiRuntimeMetrics(
            IAiExecutionMetrics execution,
            IAiRetentionMetrics retention,
            IAiStorageMetrics storage,
            IAiHotStateMetrics hotState,
            IAiResolverMetrics resolver)
        {
            Execution = execution ?? throw new ArgumentNullException(nameof(execution));
            Retention = retention ?? throw new ArgumentNullException(nameof(retention));
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            HotState = hotState ?? throw new ArgumentNullException(nameof(hotState));
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <inheritdoc />
        public IAiExecutionMetrics Execution { get; }

        /// <inheritdoc />
        public IAiRetentionMetrics Retention { get; }

        /// <inheritdoc />
        public IAiStorageMetrics Storage { get; }

        /// <inheritdoc />
        public IAiHotStateMetrics HotState { get; }

        /// <inheritdoc />
        public IAiResolverMetrics Resolver { get; }
    }
}