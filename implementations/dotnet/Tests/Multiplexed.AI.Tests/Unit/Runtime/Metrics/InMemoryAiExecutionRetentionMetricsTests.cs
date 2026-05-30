using Multiplexed.AI.Runtime.Observability.Metrics;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Metrics
{
    /// <summary>
    /// Unit tests for <see cref="InMemoryAiExecutionRetentionMetrics"/>.
    ///
    /// PURPOSE:
    /// - Validate retention metrics snapshot behavior.
    /// - Ensure evicted steps accumulate across retention runs.
    /// - Ensure latest state counters are exposed correctly.
    /// </summary>
    public sealed class InMemoryAiExecutionRetentionMetricsTests
    {
        /// <summary>
        /// Verifies that retention metrics are exposed through the snapshot.
        /// </summary>
        [Fact]
        public void Snapshot_Should_Return_Latest_Retention_Metrics()
        {
            var metrics = new InMemoryAiExecutionRetentionMetrics();

            metrics.RecordRetention(
                totalStepsBefore: 10,
                totalStepsAfter: 6,
                completedEvicted: 4,
                activeSteps: 2,
                pendingSteps: 1);

            var snapshot = metrics.Snapshot();

            Assert.Equal(10, snapshot.TotalStepsBefore);
            Assert.Equal(6, snapshot.TotalStepsAfter);
            Assert.Equal(4, snapshot.EvictedSteps);
            Assert.Equal(2, snapshot.ActiveSteps);
            Assert.Equal(1, snapshot.PendingSteps);
        }

        /// <summary>
        /// Verifies that evicted steps accumulate while latest counters are replaced.
        /// </summary>
        [Fact]
        public void RecordRetention_Should_Accumulate_EvictedSteps_And_Replace_Latest_Counters()
        {
            var metrics = new InMemoryAiExecutionRetentionMetrics();

            metrics.RecordRetention(10, 7, 3, 2, 1);
            metrics.RecordRetention(7, 5, 2, 1, 0);

            var snapshot = metrics.Snapshot();

            Assert.Equal(7, snapshot.TotalStepsBefore);
            Assert.Equal(5, snapshot.TotalStepsAfter);
            Assert.Equal(5, snapshot.EvictedSteps);
            Assert.Equal(1, snapshot.ActiveSteps);
            Assert.Equal(0, snapshot.PendingSteps);
        }
    }
}