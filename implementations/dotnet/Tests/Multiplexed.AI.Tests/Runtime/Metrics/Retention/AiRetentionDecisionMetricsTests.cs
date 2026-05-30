using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Metrics.Retention
{
    public sealed class AiRetentionDecisionMetricsTests
    {
        [Fact]
        public void RecordCompactionRequired_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordCompactionRequired("execution-1", 100, 10);
            metrics.RecordCompactionRequired("execution-2", 200, 20);

            Assert.Equal(2, metrics.CompactionRequiredCount);
        }

        [Fact]
        public void RecordEvictionRequired_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordEvictionRequired("execution-1", 100, 10);
            metrics.RecordEvictionRequired("execution-2", 200, 20);

            Assert.Equal(2, metrics.EvictionRequiredCount);
        }

        [Fact]
        public void RecordNoActionRequired_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordNoActionRequired("execution-1", 50);
            metrics.RecordNoActionRequired("execution-2", 75);

            Assert.Equal(2, metrics.NoActionRequiredCount);
        }

        [Fact]
        public void Decision_Should_Handle_Mixed_Cases()
        {
            var metrics = CreateMetrics();

            metrics.RecordCompactionRequired("execution-1", 100, 10);
            metrics.RecordEvictionRequired("execution-1", 100, 5);
            metrics.RecordNoActionRequired("execution-2", 50);

            Assert.Equal(1, metrics.CompactionRequiredCount);
            Assert.Equal(1, metrics.EvictionRequiredCount);
            Assert.Equal(1, metrics.NoActionRequiredCount);
        }

        [Fact]
        public void Decision_Should_Aggregate_Values()
        {
            var metrics = CreateMetrics();

            metrics.RecordCompactionRequired("execution-1", 100, 10);
            metrics.RecordCompactionRequired("execution-1", 100, 5);

            metrics.RecordEvictionRequired("execution-2", 200, 20);
            metrics.RecordEvictionRequired("execution-2", 200, 10);

            Assert.Equal(2, metrics.CompactionRequiredCount);
            Assert.Equal(2, metrics.EvictionRequiredCount);
        }

        [Fact]
        public void Decision_Should_Record_Global_Count_Even_With_Invalid_ExecutionId()
        {
            var metrics = CreateMetrics();

            metrics.RecordCompactionRequired("", 100, 10);
            metrics.RecordEvictionRequired(" ", 100, 10);
            metrics.RecordNoActionRequired(null!, 100);

            Assert.Equal(1, metrics.CompactionRequiredCount);
            Assert.Equal(1, metrics.EvictionRequiredCount);
            Assert.Equal(1, metrics.NoActionRequiredCount);
        }

        private static AiRetentionDecisionMetrics CreateMetrics()
        {
            return new AiRetentionDecisionMetrics(
                NoOpAiRuntimeMetricWriter.Instance);
        }
    }
}