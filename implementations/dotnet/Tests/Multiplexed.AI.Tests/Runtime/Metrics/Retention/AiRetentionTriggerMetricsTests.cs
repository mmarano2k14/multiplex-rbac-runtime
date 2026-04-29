using Multiplexed.AI.Runtime.Metrics.Retention;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Metrics.Retention
{
    public sealed class AiRetentionTriggerMetricsTests
    {
        [Fact]
        public void RecordTriggered_Should_Increment_Triggered_Count()
        {
            var metrics = new AiRetentionTriggerMetrics();

            metrics.RecordTriggered("execution-1", "retention-invoked");
            metrics.RecordTriggered("execution-2", "retention-invoked");

            Assert.Equal(2, metrics.TriggeredCount);
        }

        [Fact]
        public void RecordSkipped_Should_Increment_Skipped_Count()
        {
            var metrics = new AiRetentionTriggerMetrics();

            metrics.RecordSkipped("execution-1", "no-policy-or-no-op");
            metrics.RecordSkipped("execution-2", "no-policy-or-no-op");

            Assert.Equal(2, metrics.SkippedCount);
        }

        [Fact]
        public void RecordTriggered_Should_Ignore_ExecutionId_And_Record_Global_Count()
        {
            var metrics = new AiRetentionTriggerMetrics();

            metrics.RecordTriggered("execution-1", "retention-invoked");
            metrics.RecordTriggered("execution-1", "retention-invoked");
            metrics.RecordTriggered("execution-2", "retention-invoked");

            Assert.Equal(3, metrics.TriggeredCount);
        }

        [Fact]
        public void RecordSkipped_Should_Ignore_ExecutionId_And_Record_Global_Count()
        {
            var metrics = new AiRetentionTriggerMetrics();

            metrics.RecordSkipped("execution-1", "no-policy-or-no-op");
            metrics.RecordSkipped("execution-1", "no-policy-or-no-op");
            metrics.RecordSkipped("execution-2", "no-policy-or-no-op");

            Assert.Equal(3, metrics.SkippedCount);
        }

        [Fact]
        public void RecordTriggered_Should_Accept_Null_Or_Empty_Reason()
        {
            var metrics = new AiRetentionTriggerMetrics();

            metrics.RecordTriggered("execution-1", null!);
            metrics.RecordTriggered("execution-2", "");
            metrics.RecordTriggered("execution-3", " ");

            Assert.Equal(3, metrics.TriggeredCount);
        }

        [Fact]
        public void RecordSkipped_Should_Accept_Null_Or_Empty_Reason()
        {
            var metrics = new AiRetentionTriggerMetrics();

            metrics.RecordSkipped("execution-1", null!);
            metrics.RecordSkipped("execution-2", "");
            metrics.RecordSkipped("execution-3", " ");

            Assert.Equal(3, metrics.SkippedCount);
        }
    }
}