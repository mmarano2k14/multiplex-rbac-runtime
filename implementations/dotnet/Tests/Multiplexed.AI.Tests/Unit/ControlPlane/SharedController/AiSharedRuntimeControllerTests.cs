using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.Admission;
using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Runtime.ControlPlane.Observability;
using Multiplexed.AI.Runtime.ControlPlane.SharedController;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.SharedController
{
    public sealed class AiSharedRuntimeControllerTests
    {
        [Fact]
        public async Task SubmitRunAsync_Should_Create_Shared_Run_Assigned_To_Instance()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.AssignToInstance,
                    AssignedRuntimeInstanceId = "runtime-1",
                    AssignedInstance = CreateRuntimeInstance("runtime-1"),
                    Reason = "Runtime instance selected.",
                    VisibleInstanceCount = 1,
                    AvailableInstanceCount = 1,
                    CurrentInstanceCount = 1
                });

            var controller = CreateController(admission);

            var result = await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest(),
                RequestedBy = "tester",
                Source = "unit-test"
            });

            Assert.True(result.Success);
            Assert.Equal("shared-run-1", result.SharedRunId);
            Assert.NotNull(result.Run);
            Assert.Equal(AiSharedRunStatus.AssignedToInstance, result.Run.Status);
            Assert.Equal("runtime-1", result.AssignedRuntimeInstanceId);
            Assert.True(admission.AdmitCalled);
            Assert.Equal("shared-run-1", admission.LastRequest?.RunId);
        }

        [Fact]
        public async Task SubmitRunAsync_Should_Create_QueuedGlobally_Run_When_Admission_Queues_Globally()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.QueueGlobally,
                    Reason = "No local capacity.",
                    VisibleInstanceCount = 1,
                    AvailableInstanceCount = 0,
                    CurrentInstanceCount = 1
                });

            var controller = CreateController(admission);

            var result = await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Run);
            Assert.Equal(AiSharedRunStatus.QueuedGlobally, result.Run.Status);
            Assert.Null(result.AssignedRuntimeInstanceId);
        }

        [Fact]
        public async Task SubmitRunAsync_Should_Create_ScaleOutRequested_Run_When_Admission_Requests_ScaleOut()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.RequestScaleOut,
                    Reason = "Scale-out required.",
                    VisibleInstanceCount = 1,
                    AvailableInstanceCount = 0,
                    CurrentInstanceCount = 1,
                    MaxInstanceCount = 3
                });

            var controller = CreateController(admission);

            var result = await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Run);
            Assert.Equal(AiSharedRunStatus.ScaleOutRequested, result.Run.Status);
        }

        [Fact]
        public async Task SubmitRunAsync_Should_Create_Rejected_Run_When_Admission_Rejects()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.Reject,
                    Reason = "No capacity.",
                    VisibleInstanceCount = 1,
                    AvailableInstanceCount = 0,
                    CurrentInstanceCount = 1
                });

            var controller = CreateController(admission);

            var result = await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Run);
            Assert.Equal(AiSharedRunStatus.Rejected, result.Run.Status);
            Assert.Equal("No capacity.", result.Run.FailureReason);
        }

        [Fact]
        public async Task GetRunAsync_Should_Return_Known_Run()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.AssignToInstance,
                    AssignedRuntimeInstanceId = "runtime-1",
                    AssignedInstance = CreateRuntimeInstance("runtime-1")
                });

            var controller = CreateController(admission);

            await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            var result = await controller.GetRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.GetRun,
                SharedRunId = "shared-run-1"
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Run);
            Assert.Equal("shared-run-1", result.Run.SharedRunId);
        }

        [Fact]
        public async Task ListRunsAsync_Should_Return_Known_Runs()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.AssignToInstance,
                    AssignedRuntimeInstanceId = "runtime-1",
                    AssignedInstance = CreateRuntimeInstance("runtime-1")
                });

            var controller = CreateController(admission);

            await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            var result = await controller.ListRunsAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.ListRuns
            });

            Assert.True(result.Success);
            Assert.Single(result.Runs);
            Assert.Equal("shared-run-1", result.Runs[0].SharedRunId);
        }

        [Fact]
        public async Task CancelRunAsync_Should_Mark_Run_As_Cancelled()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.QueueGlobally,
                    Reason = "Queued globally."
                });

            var controller = CreateController(admission);

            await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            var result = await controller.CancelRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.CancelRun,
                SharedRunId = "shared-run-1",
                Reason = "operator cancel",
                RequestedBy = "tester"
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Run);
            Assert.Equal(AiSharedRunStatus.Cancelled, result.Run.Status);
            Assert.Equal("operator cancel", result.Run.FailureReason);
            Assert.Equal("tester", result.Run.RequestedBy);
        }

        [Fact]
        public async Task CancelRunAsync_Should_Return_Same_Run_When_Already_Cancelled()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.QueueGlobally
                });

            var controller = CreateController(admission);

            await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            await controller.CancelRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.CancelRun,
                SharedRunId = "shared-run-1",
                Reason = "first cancel"
            });

            var result = await controller.CancelRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.CancelRun,
                SharedRunId = "shared-run-1",
                Reason = "second cancel"
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Run);
            Assert.Equal(AiSharedRunStatus.Cancelled, result.Run.Status);
            Assert.Equal("first cancel", result.Run.FailureReason);
        }

        [Fact]
        public async Task ExecuteAsync_Should_Dispatch_By_Operation()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.QueueGlobally
                });

            var controller = CreateController(admission);

            var result = await controller.ExecuteAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest()
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Run);
            Assert.Equal(AiSharedRuntimeControllerOperation.SubmitRun, result.Operation);
        }

        [Fact]
        public async Task SubmitRunAsync_Should_Return_Failure_When_RunRequest_Is_Missing()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.QueueGlobally
                });

            var controller = CreateController(admission);

            var result = await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun
            });

            Assert.False(result.Success);
            Assert.Contains("RunRequest is required", result.FailureReason);
        }

        [Fact]
        public async Task GetRunAsync_Should_Return_Failure_When_SharedRunId_Is_Missing()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.QueueGlobally
                });

            var controller = CreateController(admission);

            var result = await controller.GetRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.GetRun
            });

            Assert.False(result.Success);
            Assert.Contains("SharedRunId is required", result.FailureReason);
        }

        [Fact]
        public async Task SubmitRunAsync_Should_Return_Failure_When_Disabled()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.AssignToInstance,
                    AssignedRuntimeInstanceId = "runtime-1"
                });

            var controller = CreateController(
                admission,
                new AiSharedRuntimeControllerOptions
                {
                    EnableSubmitRun = false
                });

            var result = await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RunRequest = CreateRunRequest()
            });

            Assert.False(result.Success);
            Assert.Contains("disabled", result.FailureReason);
        }

        [Fact]
        public async Task SubmitRunAsync_Should_Record_Started_And_Completed_Events()
        {
            var admission = new FakeRunAdmissionController(
                new AiRunAdmissionDecision
                {
                    DecisionType = AiRunAdmissionDecisionType.AssignToInstance,
                    AssignedRuntimeInstanceId = "runtime-1",
                    AssignedInstance = CreateRuntimeInstance("runtime-1")
                });

            var observer = new CapturingControlPlaneObserver();

            var controller = new AiSharedRuntimeController(
                admission,
                new InMemoryAiSharedRunStore(),
                Options.Create(new AiSharedRuntimeControllerOptions()),
                observer);

            var result = await controller.SubmitRunAsync(new AiSharedRuntimeControllerRequest
            {
                Operation = AiSharedRuntimeControllerOperation.SubmitRun,
                RequestedSharedRunId = "shared-run-1",
                RunRequest = CreateRunRequest(),
                CorrelationId = "correlation-1",
                RequestedBy = "tester",
                Source = "unit-test"
            });

            Assert.True(result.Success);
            Assert.Equal(2, observer.Events.Count);

            Assert.Equal(AiControlPlaneEventType.OperationStarted, observer.Events[0].EventType);
            Assert.Equal(AiControlPlaneEventType.OperationCompleted, observer.Events[1].EventType);

            Assert.All(observer.Events, controlPlaneEvent =>
            {
                Assert.Equal(AiControlPlaneArea.SharedController, controlPlaneEvent.Area);
                Assert.Equal("SubmitRun", controlPlaneEvent.Operation);
                Assert.Equal("correlation-1", controlPlaneEvent.Correlation.CorrelationId);
                Assert.Equal("shared-run-1", controlPlaneEvent.Correlation.RunId);
            });
        }

        private static AiSharedRuntimeController CreateController(
            IAiRunAdmissionController admissionController,
            AiSharedRuntimeControllerOptions? options = null)
        {
            return new AiSharedRuntimeController(
                admissionController,
                new InMemoryAiSharedRunStore(),
                Options.Create(options ?? new AiSharedRuntimeControllerOptions()),
                new NoopAiControlPlaneObserver());
        }

        private static AiRuntimePipelineRunRequest CreateRunRequest()
        {
            return new AiRuntimePipelineRunRequest
            {
                PipelineName = "pipeline-1"
            };
        }

        private static AiRuntimeInstanceSnapshot CreateRuntimeInstance(
            string runtimeInstanceId)
        {
            var now = DateTimeOffset.UtcNow;

            return new AiRuntimeInstanceSnapshot
            {
                RuntimeInstanceId = runtimeInstanceId,
                Status = AiRuntimeInstanceStatus.Ready,
                WorkerCount = 4,
                QueuedRunCount = 0,
                RunningRunCount = 0,
                ActiveRunCount = 0,
                QueueCapacity = 8,
                MaxConcurrentRuns = 2,
                AvailableRunSlots = 2,
                IsQueuePaused = false,
                CanAcceptRun = true,
                RegisteredAtUtc = now,
                LastHeartbeatAtUtc = now,
                SnapshotAtUtc = now
            };
        }

        private sealed class CapturingControlPlaneObserver : IAiControlPlaneObserver
        {
            public List<AiControlPlaneEvent> Events { get; } = new();

            public Task RecordAsync(
                AiControlPlaneEvent controlPlaneEvent,
                CancellationToken cancellationToken = default)
            {
                Events.Add(controlPlaneEvent);

                return Task.CompletedTask;
            }
        }

        private sealed class FakeRunAdmissionController : IAiRunAdmissionController
        {
            private readonly AiRunAdmissionDecision _decision;

            public FakeRunAdmissionController(
                AiRunAdmissionDecision decision)
            {
                _decision = decision;
            }

            public bool AdmitCalled { get; private set; }

            public AiRunAdmissionRequest? LastRequest { get; private set; }

            public Task<AiRunAdmissionDecision> AdmitAsync(
                AiRunAdmissionRequest request,
                CancellationToken cancellationToken = default)
            {
                AdmitCalled = true;
                LastRequest = request;

                return Task.FromResult(_decision);
            }
        }
    }
}