using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Observability.Metrics.Retention;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Metrics.Retention
{
    public sealed class AiRetentionPlanMetricsTests
    {
        [Fact]
        public void RecordPlanCreated_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordPlanCreated("execution-1", 10, 5, 85);
            metrics.RecordPlanCreated("execution-2", 20, 10, 70);

            Assert.Equal(2, metrics.PlanCreatedCount);
        }

        [Fact]
        public void RecordPlanCreated_Should_Aggregate_Values()
        {
            var metrics = CreateMetrics();

            metrics.RecordPlanCreated("execution-1", 10, 5, 85);
            metrics.RecordPlanCreated("execution-1", 20, 10, 70);

            Assert.Equal(2, metrics.PlanCreatedCount);
            Assert.Equal(30, metrics.TotalCompactedSteps);
            Assert.Equal(15, metrics.TotalEvictedSteps);
            Assert.Equal(155, metrics.TotalKeepSteps);
        }

        [Fact]
        public void RecordPlanCreated_Should_Handle_Zero_Values()
        {
            var metrics = CreateMetrics();

            metrics.RecordPlanCreated("execution-1", 0, 0, 100);

            Assert.Equal(1, metrics.PlanCreatedCount);
            Assert.Equal(0, metrics.TotalCompactedSteps);
            Assert.Equal(0, metrics.TotalEvictedSteps);
            Assert.Equal(100, metrics.TotalKeepSteps);
        }

        [Fact]
        public void RecordPlanCreated_Should_Accept_Invalid_ExecutionId()
        {
            var metrics = CreateMetrics();

            metrics.RecordPlanCreated("", 10, 5, 85);
            metrics.RecordPlanCreated(" ", 20, 10, 70);
            metrics.RecordPlanCreated(null!, 5, 2, 50);

            Assert.Equal(3, metrics.PlanCreatedCount);
        }

        [Fact]
        public void PlanMetrics_Should_Handle_Mixed_Cases()
        {
            var metrics = CreateMetrics();

            metrics.RecordPlanCreated("execution-1", 10, 0, 90);
            metrics.RecordPlanCreated("execution-2", 0, 5, 95);
            metrics.RecordPlanCreated("execution-3", 0, 0, 100);

            Assert.Equal(3, metrics.PlanCreatedCount);
            Assert.Equal(10, metrics.TotalCompactedSteps);
            Assert.Equal(5, metrics.TotalEvictedSteps);
            Assert.Equal(285, metrics.TotalKeepSteps);
        }

        private static AiRetentionPlanMetrics CreateMetrics()
        {
            return new AiRetentionPlanMetrics(
                NoOpAiRuntimeMetricWriter.Instance);
        }
    }
}