using Multiplexed.AI.Runtime.Metrics.Resolvers;
using System;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Metrics.Resolvers
{
    public sealed class AiResolverMetricsTests
    {
        [Fact]
        public void RecordResolveStarted_Should_Increment_Count()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveStarted("execution-1", "step-1", "state.input");
            metrics.RecordResolveStarted("execution-1", "step-2", "state.input");

            Assert.Equal(2, metrics.ResolvedStartedCount);
        }

        [Fact]
        public void RecordResolveSuccess_Should_Increment_Count()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveSuccess("execution-1", "step-1", "state.input");
            metrics.RecordResolveSuccess("execution-1", "step-2", "steps.step-1.result");

            Assert.Equal(2, metrics.ResolvedSuccessCount);
        }

        [Fact]
        public void RecordResolveMiss_Should_Increment_Count()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveMiss("execution-1", "step-1", "state.missing");
            metrics.RecordResolveMiss("execution-1", "step-2", "steps.unknown.result");

            Assert.Equal(2, metrics.ResolvedMissCount);
        }

        [Fact]
        public void RecordResolveFailed_Should_Increment_Count_And_Group_By_Exception()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveFailed("execution-1", "step-1", "state.input", new InvalidOperationException());
            metrics.RecordResolveFailed("execution-2", "step-2", "state.input", new InvalidOperationException());
            metrics.RecordResolveFailed("execution-3", "step-3", "state.job", new ArgumentException());

            Assert.Equal(3, metrics.ResolvedFailedCount);
            Assert.Equal(2, metrics.FailuresByExceptionType["InvalidOperationException"]);
            Assert.Equal(1, metrics.FailuresByExceptionType["ArgumentException"]);
        }

        [Fact]
        public void ResolverMetrics_Should_Group_Operations_By_Path()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveStarted("execution-1", "step-1", "state.input");
            metrics.RecordResolveSuccess("execution-1", "step-1", "state.input");
            metrics.RecordResolveMiss("execution-1", "step-1", "state.missing");

            Assert.Equal(2, metrics.OperationsByPath["state.input"]);
            Assert.Equal(1, metrics.OperationsByPath["state.missing"]);
        }

        [Fact]
        public void ResolverMetrics_Should_Normalize_Path()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveStarted("execution-1", "step-1", "");
            metrics.RecordResolveSuccess("execution-1", "step-1", " ");
            metrics.RecordResolveMiss("execution-1", "step-1", null!);

            Assert.Equal(3, metrics.OperationsByPath["unknown"]);
        }

        [Fact]
        public void ResolverMetrics_Should_Trim_Path()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveStarted("execution-1", "step-1", " state.input ");
            metrics.RecordResolveSuccess("execution-1", "step-1", "state.input");

            Assert.Equal(2, metrics.OperationsByPath["state.input"]);
        }

        [Fact]
        public void ResolverMetrics_Should_Handle_Mixed_Cases()
        {
            var metrics = new AiResolverMetrics();

            metrics.RecordResolveStarted("execution-1", "step-1", "state.input");
            metrics.RecordResolveSuccess("execution-1", "step-1", "state.input");
            metrics.RecordResolveMiss("execution-1", "step-1", "state.missing");
            metrics.RecordResolveFailed("execution-1", "step-1", "state.error", new Exception("fail"));

            Assert.Equal(1, metrics.ResolvedStartedCount);
            Assert.Equal(1, metrics.ResolvedSuccessCount);
            Assert.Equal(1, metrics.ResolvedMissCount);
            Assert.Equal(1, metrics.ResolvedFailedCount);
        }
    }
}