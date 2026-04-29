using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Execution;
using Multiplexed.AI.Runtime.Metrics.HotState;
using Multiplexed.AI.Runtime.Metrics.Resolvers;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Multiplexed.AI.Runtime.Metrics.Storage;
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
                new AiResolverMetrics()
            );
        }
    }
}
