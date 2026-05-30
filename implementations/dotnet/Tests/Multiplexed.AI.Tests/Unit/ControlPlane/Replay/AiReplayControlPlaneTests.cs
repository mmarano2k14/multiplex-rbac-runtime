using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Reports;
using Multiplexed.AI.Runtime.ControlPlane.Replay;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.Replay
{
    public sealed class AiReplayControlPlaneTests
    {
        [Fact]
        public async Task ReplayAsync_Should_Map_To_AuditOnly_Mode()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.ReplayAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Replay
            });

            Assert.True(result.Success);
            Assert.Equal("execution-1", fakeReplayService.LastRequest?.ExecutionId);
            Assert.Equal(AiExecutionReplayMode.AuditOnly, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task AuditAsync_Should_Map_To_AuditOnly_Mode()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.AuditAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Audit
            });

            Assert.True(result.Success);
            Assert.Equal(AiExecutionReplayMode.AuditOnly, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task RestoreAsync_Should_Map_To_ResumeIncomplete_Mode()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.RestoreAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Restore
            });

            Assert.True(result.Success);
            Assert.Equal(AiExecutionReplayMode.ResumeIncomplete, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task GetReportAsync_Should_Map_To_AuditOnly_Mode()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.GetReportAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.GetReport
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Report);
            Assert.Equal(AiExecutionReplayMode.AuditOnly, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task GetLedgerAsync_Should_Map_To_AuditOnly_Mode()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.GetLedgerAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.GetLedger
            });

            Assert.True(result.Success);
            Assert.Equal(AiExecutionReplayMode.AuditOnly, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task GetTimelineAsync_Should_Map_To_AuditOnly_Mode()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.GetTimelineAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.GetTimeline
            });

            Assert.True(result.Success);
            Assert.Equal(AiExecutionReplayMode.AuditOnly, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task ExecuteAsync_Should_Dispatch_By_Operation()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.ExecuteAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Restore
            });

            Assert.True(result.Success);
            Assert.Equal(AiReplayOperation.Restore, result.Operation);
            Assert.Equal(AiExecutionReplayMode.ResumeIncomplete, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task ReplayAsync_Should_Return_Failure_Result_When_ExecutionId_Is_Empty()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.ReplayAsync(new AiReplayControlRequest
            {
                ExecutionId = string.Empty,
                Operation = AiReplayOperation.Replay
            });

            Assert.False(result.Success);
            Assert.Equal(AiReplayOperation.Replay, result.Operation);
            Assert.Contains("ExecutionId is required", result.FailureReason);
        }

        [Fact]
        public async Task ReplayAsync_Should_Return_Failure_Result_When_Replay_Is_Disabled()
        {
            var fakeReplayService = new FakeExecutionReplayService();

            var controlPlane = CreateControlPlane(
                fakeReplayService,
                new AiReplayControlOptions
                {
                    EnableReplay = false
                });

            var result = await controlPlane.ReplayAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Replay
            });

            Assert.False(result.Success);
            Assert.Contains("disabled", result.FailureReason);
        }

        [Fact]
        public async Task ReplayAsync_Should_Not_Expose_ReExecuteAll_Mode()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            await controlPlane.ReplayAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Replay
            });

            Assert.NotEqual(AiExecutionReplayMode.ReExecuteAll, fakeReplayService.LastRequest?.Mode);
        }

        [Fact]
        public async Task ReplayAsync_Should_Propagate_Correlation_Metadata()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.ReplayAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Replay,
                CorrelationId = "correlation-1",
                RuntimeInstanceId = "runtime-instance-1",
                RequestedBy = "operator-1"
            });

            Assert.True(result.Success);
            Assert.Equal("correlation-1", result.CorrelationId);
            Assert.Equal("runtime-instance-1", result.RuntimeInstanceId);
            Assert.Equal("operator-1", result.RequestedBy);
        }

        [Fact]
        public async Task ReplayAsync_Should_Set_Duration_And_Timestamps()
        {
            var fakeReplayService = new FakeExecutionReplayService();
            var controlPlane = CreateControlPlane(fakeReplayService);

            var result = await controlPlane.ReplayAsync(new AiReplayControlRequest
            {
                ExecutionId = "execution-1",
                Operation = AiReplayOperation.Replay
            });

            Assert.True(result.StartedAtUtc <= result.CompletedAtUtc);
            Assert.True(result.DurationMs >= 0);
        }

        private static AiReplayControlPlane CreateControlPlane(
            IAiExecutionReplayService replayService,
            AiReplayControlOptions? options = null)
        {
            return new AiReplayControlPlane(
                replayService,
                Options.Create(options ?? new AiReplayControlOptions()));
        }

        private sealed class FakeExecutionReplayService : IAiExecutionReplayService
        {
            public AiExecutionReplayRequest? LastRequest { get; private set; }

            public Task<AiExecutionReplayReport> ReplayAsync(
                AiExecutionReplayRequest request,
                CancellationToken cancellationToken = default)
            {
                LastRequest = request;

                return Task.FromResult(new AiExecutionReplayReport
                {
                    ExecutionId = request.ExecutionId,
                    Mode = request.Mode,
                    ExecutionFound = true,
                    SnapshotFound = true,
                    ReplayValid = true
                });
            }
        }
    }
}