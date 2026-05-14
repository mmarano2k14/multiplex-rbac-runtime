using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Execution;
using Multiplexed.AI.Runtime.Metrics.HotState;
using Multiplexed.AI.Runtime.Metrics.Policy;
using Multiplexed.AI.Runtime.Metrics.Resolvers;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Multiplexed.AI.Runtime.Metrics.Storage;
using Multiplexed.AI.Runtime.Metrics.Workers;
using Multiplexed.AI.Runtime.Observability;
using Multiplexed.AI.Runtime.Tracing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Tests.Integration.Helpers
{
    public static class MetricsFactory
    {
        public static IAiRuntimeMetrics Create()
        {
            return new AiRuntimeMetrics(
                new AiExecutionMetrics(),
                new AiRetentionMetrics(
                    new AiRetentionTriggerMetrics(),
                    new AiRetentionDecisionMetrics(),
                    new AiRetentionPlanMetrics(),
                    new AiRetentionExecutionMetrics()
                ),
                new AiStorageMetrics(),
                new AiHotStateMetrics(),
                new AiResolverMetrics(),
                new AiPolicyMetrics(),
                new AiRuntimeInstanceWorkerMetrics()
            );
        }


        /// <summary>
        /// Factory helper for creating a fully configured <see cref="IAiRuntimeObservability"/> instance.
        /// </summary>
        /// <remarks>
        /// PURPOSE:
        /// - Provides a simple way to construct observability without DI.
        /// - Used in tests, local runtime setups, and lightweight execution contexts.
        ///
        /// DESIGN:
        /// - Composes Metrics, Tracer, and Logger into a single observability instance.
        /// - Uses in-memory metrics and no-op tracing by default.
        ///
        /// IMPORTANT:
        /// - This factory is not intended for production DI usage.
        /// - Production environments should rely on IServiceCollection configuration.
        /// </remarks>
        public static class ObservabilityFactory
        {
            /// <summary>
            /// Creates a default <see cref="IAiRuntimeObservability"/> instance.
            /// </summary>
            /// <returns>A fully initialized observability instance.</returns>
            public static IAiRuntimeObservability Create()
            {
                // ------------------------------------------------------------
                // Metrics
                // ------------------------------------------------------------

                var metrics = new AiRuntimeMetrics(
                    new AiExecutionMetrics(),
                    new AiRetentionMetrics(
                        new AiRetentionTriggerMetrics(),
                        new AiRetentionDecisionMetrics(),
                        new AiRetentionPlanMetrics(),
                        new AiRetentionExecutionMetrics()
                    ),
                    new AiStorageMetrics(),
                    new AiHotStateMetrics(),
                    new AiResolverMetrics(),
                    new AiPolicyMetrics(),
                    new AiRuntimeInstanceWorkerMetrics()
                );

                // ------------------------------------------------------------
                // Tracing (NoOp by default)
                // ------------------------------------------------------------

                IAiRuntimeTracer tracer = new NoOpAiRuntimeTracer();

                // ------------------------------------------------------------
                // Logger (runtime default)
                // ------------------------------------------------------------

                IAiRuntimeLogger logger = new NoopLogger();

                // ------------------------------------------------------------
                // Observability
                // ------------------------------------------------------------

                return new AiRuntimeObservability(
                    metrics,
                    tracer,
                    logger
                );
            }
        }
    }
}
