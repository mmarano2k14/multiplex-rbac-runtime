using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Execution;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Metrics
{
    /// <summary>
    /// Unit tests for <see cref="AiRuntimeMetrics"/>.
    ///
    /// PURPOSE:
    /// - Validate in-memory runtime counters.
    /// - Ensure retry, recovery, claim, and finalization metrics are observable.
    /// - Protect against regressions in hot-path metrics used by the DAG runtime.
    /// </summary>
    public sealed class AiRuntimeMetricsTests
    {
        /// <summary>
        /// Verifies that retry counts are accumulated per step name.
        /// </summary>
        [Fact]
        public void IncrementRetry_Should_Record_Retry_Count_Per_Step()
        {
            var metrics = new AiExecutionMetrics();

            metrics.RecordStepRetried("execution-1", "step-1");
            metrics.RecordStepRetried("execution-1", "step-1");
            metrics.RecordStepRetried("execution-1", "step-2");

            var snapshot = metrics.GetRetryByStep();

            Assert.Equal(2, snapshot["step-1"]);
            Assert.Equal(1, snapshot["step-2"]);
        }

        /// <summary>
        /// Verifies that recovery counts are accumulated per execution id.
        /// </summary>
        [Fact]
        public void RecordStepsRecovered_Should_Record_Recovered_Count_Per_Execution()
        {
            var metrics = new AiExecutionMetrics();

            metrics.RecordStepsRecovered("execution-1", 2);
            metrics.RecordStepsRecovered("execution-1", 3);
            metrics.RecordStepsRecovered("execution-2", 1);

            var snapshot = metrics.GetRecoveryByExecution();

            Assert.Equal(5, snapshot["execution-1"]);
            Assert.Equal(1, snapshot["execution-2"]);
        }

        /// <summary>
        /// Verifies global finalization counters.
        /// </summary>
        [Fact]
        public void Finalize_Counters_Should_Be_Incremented()
        {
            var metrics = new AiExecutionMetrics();

            metrics.RecordFinalizeAttempt("execution-1");
            metrics.RecordFinalizeAttempt("execution-1");
            metrics.RecordFinalizeSuccess("execution-1");

            Assert.Equal(2, metrics.FinalizeAttemptCount);
            Assert.Equal(1, metrics.FinalizeSuccessCount);
        }

        /// <summary>
        /// Verifies claim success counts per step and global claim miss count.
        /// </summary>
        [Fact]
        public void Claim_Counters_Should_Be_Recorded()
        {
            var metrics = new AiExecutionMetrics();

            metrics.RecordStepClaimed("execution-1", "step-1");
            metrics.RecordStepClaimed("execution-1", "step-1");
            metrics.RecordStepClaimed("execution-1", "step-2");

            metrics.RecordStepClaimMiss("execution-1");
            metrics.RecordStepClaimMiss("execution-1");

            var claims = metrics.GetClaimSuccessByStep();

            Assert.Equal(2, claims["step-1"]);
            Assert.Equal(1, claims["step-2"]);
            Assert.Equal(2, metrics.StepClaimMissCount);
        }

        /// <summary>
        /// Verifies that invalid metric keys do not create entries.
        /// </summary>
        [Fact]
        public void Metrics_Should_Ignore_Invalid_Keys()
        {
            var metrics = new AiExecutionMetrics();

            metrics.RecordStepRetried("execution-1", "");
            metrics.RecordStepRetried("execution-1", " ");

            metrics.RecordStepClaimed("execution-1", "");

            metrics.RecordStepsRecovered("", 1);
            metrics.RecordStepsRecovered("execution-1", 0);
            metrics.RecordStepsRecovered("execution-1", -1);

            Assert.Empty(metrics.GetRetryByStep());
            Assert.Empty(metrics.GetClaimSuccessByStep());
            Assert.Empty(metrics.GetRecoveryByExecution());
        }
    }
}