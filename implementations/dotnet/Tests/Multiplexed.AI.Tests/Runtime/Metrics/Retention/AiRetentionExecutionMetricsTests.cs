using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Retention;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Metrics.Retention
{
    public sealed class AiRetentionExecutionMetricsTests
    {
        [Fact]
        public void RecordStepEvicted_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordStepEvicted("execution-1", "step-1");
            metrics.RecordStepEvicted("execution-1", "step-2");

            Assert.Equal(2, metrics.StepEvictedCount);
        }

        [Fact]
        public void RecordStepMarkedArchived_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordStepMarkedArchived("execution-1", "step-1");
            metrics.RecordStepMarkedArchived("execution-1", "step-2");

            Assert.Equal(2, metrics.StepMarkedArchivedCount);
        }

        [Fact]
        public void RecordPayloadCompacted_Should_Accumulate_Bytes()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadCompacted("execution-1", "step-1", 1000, 500);
            metrics.RecordPayloadCompacted("execution-1", "step-2", 2000, 1000);

            Assert.Equal(3000, metrics.TotalBeforeBytes);
            Assert.Equal(1500, metrics.TotalAfterBytes);
            Assert.Equal(1500, metrics.TotalBytesSaved);
            Assert.Equal(2, metrics.PayloadCompactedCount);
        }

        [Fact]
        public void RecordRetentionCompleted_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordRetentionCompleted("execution-1");
            metrics.RecordRetentionCompleted("execution-2");

            Assert.Equal(2, metrics.RetentionCompletedCount);
        }

        [Fact]
        public void RecordRetentionFailed_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordRetentionFailed("execution-1", new System.Exception("fail"));
            metrics.RecordRetentionFailed("execution-2", new System.Exception("fail"));

            Assert.Equal(2, metrics.RetentionFailedCount);
            Assert.True(metrics.FailuresByExceptionType.Count > 0);
        }

        [Fact]
        public void ExecutionMetrics_Should_Handle_Mixed_Cases()
        {
            var metrics = CreateMetrics();

            metrics.RecordStepEvicted("execution-1", "step-1");
            metrics.RecordStepMarkedArchived("execution-1", "step-1");

            metrics.RecordPayloadCompacted("execution-1", "step-2", 1000, 500);

            metrics.RecordRetentionCompleted("execution-1");

            Assert.Equal(1, metrics.StepEvictedCount);
            Assert.Equal(1, metrics.StepMarkedArchivedCount);
            Assert.Equal(1000, metrics.TotalBeforeBytes);
            Assert.Equal(500, metrics.TotalAfterBytes);
            Assert.Equal(500, metrics.TotalBytesSaved);
            Assert.Equal(1, metrics.RetentionCompletedCount);
        }

        [Fact]
        public void ExecutionMetrics_Should_Accept_Invalid_ExecutionId()
        {
            var metrics = CreateMetrics();

            metrics.RecordStepEvicted("", "step-1");
            metrics.RecordStepMarkedArchived(" ", "step-1");
            metrics.RecordRetentionCompleted(null!);

            Assert.Equal(1, metrics.StepEvictedCount);
            Assert.Equal(1, metrics.StepMarkedArchivedCount);
            Assert.Equal(1, metrics.RetentionCompletedCount);
        }

        private static AiRetentionExecutionMetrics CreateMetrics()
        {
            return new AiRetentionExecutionMetrics(
                NoOpAiRuntimeMetricWriter.Instance);
        }
    }
}