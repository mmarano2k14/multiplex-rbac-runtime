using Multiplexed.AI.Runtime.Metrics.HotState;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Metrics.HotState
{
    public sealed class AiHotStateMetricsTests
    {
        [Fact]
        public void RecordStateStepAdded_Should_Increment_Count()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateStepAdded("execution-1", "step-1");
            metrics.RecordStateStepAdded("execution-1", "step-2");

            Assert.Equal(2, metrics.StateStepAddedCount);
        }

        [Fact]
        public void RecordStateStepRemoved_Should_Increment_Count()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateStepRemoved("execution-1", "step-1");
            metrics.RecordStateStepRemoved("execution-1", "step-2");

            Assert.Equal(2, metrics.StateStepRemovedCount);
        }

        [Fact]
        public void RecordStateCompacted_Should_Record_Count_And_Last_Values()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateCompacted("execution-1", 100, 75);

            Assert.Equal(1, metrics.StateCompactedCount);
            Assert.Equal(100, metrics.LastCompactionBeforeSteps);
            Assert.Equal(75, metrics.LastCompactionAfterSteps);
            Assert.Equal(25, metrics.TotalStepsRemovedByCompaction);
        }

        [Fact]
        public void RecordStateCompacted_Should_Accumulate_Removed_Steps()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateCompacted("execution-1", 100, 80);
            metrics.RecordStateCompacted("execution-2", 50, 40);

            Assert.Equal(2, metrics.StateCompactedCount);
            Assert.Equal(50, metrics.LastCompactionBeforeSteps);
            Assert.Equal(40, metrics.LastCompactionAfterSteps);
            Assert.Equal(30, metrics.TotalStepsRemovedByCompaction);
        }

        [Fact]
        public void RecordStateSizeObserved_Should_Record_Last_Observed_Values()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateSizeObserved("execution-1", 42, 4096);

            Assert.Equal(1, metrics.StateSizeObservedCount);
            Assert.Equal(42, metrics.LastObservedStepCount);
            Assert.Equal(4096, metrics.LastEstimatedBytes);
        }

        [Fact]
        public void RecordStateSizeObserved_Should_Not_Reset_Bytes_When_Null()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateSizeObserved("execution-1", 42, 4096);
            metrics.RecordStateSizeObserved("execution-1", 10, null);

            Assert.Equal(2, metrics.StateSizeObservedCount);
            Assert.Equal(10, metrics.LastObservedStepCount);
            Assert.Equal(4096, metrics.LastEstimatedBytes);
        }

        [Fact]
        public void HotStateMetrics_Should_Normalize_Negative_Values()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateCompacted("execution-1", -100, -50);
            metrics.RecordStateSizeObserved("execution-1", -10, -500);

            Assert.Equal(0, metrics.LastCompactionBeforeSteps);
            Assert.Equal(0, metrics.LastCompactionAfterSteps);
            Assert.Equal(0, metrics.TotalStepsRemovedByCompaction);
            Assert.Equal(0, metrics.LastObservedStepCount);
            Assert.Equal(0, metrics.LastEstimatedBytes);
        }

        [Fact]
        public void HotStateMetrics_Should_Accept_Invalid_ExecutionId()
        {
            var metrics = new AiHotStateMetrics();

            metrics.RecordStateStepAdded("", "step-1");
            metrics.RecordStateStepRemoved(" ", "step-1");
            metrics.RecordStateCompacted(null!, 10, 5);
            metrics.RecordStateSizeObserved(null!, 3, 100);

            Assert.Equal(1, metrics.StateStepAddedCount);
            Assert.Equal(1, metrics.StateStepRemovedCount);
            Assert.Equal(1, metrics.StateCompactedCount);
            Assert.Equal(1, metrics.StateSizeObservedCount);
        }
    }
}