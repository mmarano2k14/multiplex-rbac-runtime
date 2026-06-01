using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Unit tests for <see cref="AiExecutionAssistanceCoordinator"/>.
    /// </summary>
    public sealed class AiExecutionAssistanceCoordinatorTests
    {
        /// <summary>
        /// Validates that the coordinator does not evaluate assistance when disabled.
        /// </summary>
        [Fact]
        public async Task RunOnceAsync_Should_Not_Evaluate_When_Assistance_Is_Disabled()
        {
            var coordinator = CreateCoordinator(
                 configureOptions: options => options.Enabled = false);

            var result = await coordinator.RunOnceAsync(
                helperRuntimeInstanceId: "runtime-helper-1",
                helperIsIdle: true,
                helperQueueDepth: 0,
                helperAvailableWorkerSlots: 1);

            Assert.False(result.Enabled);
            Assert.Equal(0, result.CandidateCount);
            Assert.Empty(result.Decisions);
            Assert.Empty(result.PumpResults);
        }

        /// <summary>
        /// Validates that the coordinator skips evaluation when the helper is not idle.
        /// </summary>
        [Fact]
        public async Task RunOnceAsync_Should_Skip_When_Helper_Is_Not_Idle()
        {
            var coordinator = CreateCoordinator();

            var result = await coordinator.RunOnceAsync(
                helperRuntimeInstanceId: "runtime-helper-1",
                helperIsIdle: false,
                helperQueueDepth: 1,
                helperAvailableWorkerSlots: 1);

            Assert.True(result.Enabled);
            Assert.Equal(0, result.CandidateCount);
            Assert.Empty(result.Decisions);
            Assert.Empty(result.PumpResults);
        }

        /// <summary>
        /// Validates that the coordinator skips candidates owned by the same helper runtime instance.
        /// </summary>
        [Fact]
        public async Task RunOnceAsync_Should_Skip_Candidate_Owned_By_Helper()
        {
            var candidateStore = new InMemoryAiExecutionAssistanceCandidateStore();

            await candidateStore.UpsertAsync(
                CreateCandidate(
                    primaryRuntimeInstanceId: "runtime-helper-1"));

            var coordinator = CreateCoordinator(
                candidateStore: candidateStore);

            var result = await coordinator.RunOnceAsync(
                helperRuntimeInstanceId: "runtime-helper-1",
                helperIsIdle: true,
                helperQueueDepth: 0,
                helperAvailableWorkerSlots: 1);

            Assert.True(result.Enabled);
            Assert.Equal(1, result.CandidateCount);
            Assert.Equal(1, result.SkippedCandidateCount);
            Assert.Empty(result.Decisions);
            Assert.Empty(result.PumpResults);
        }

        /// <summary>
        /// Validates that the coordinator grants a lease and starts the pump for a valid candidate.
        /// </summary>
        [Fact]
        public async Task RunOnceAsync_Should_Grant_Lease_And_Start_Pump_When_Candidate_Is_Valid()
        {
            var candidateStore = new InMemoryAiExecutionAssistanceCandidateStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();

            await candidateStore.UpsertAsync(
                CreateCandidate(
                    estimatedReadyStepCount: 50,
                    estimatedRemainingStepCount: 200,
                    estimatedActiveWorkerCount: 1));

            var coordinator = CreateCoordinator(
                candidateStore: candidateStore,
                runtimeWorker: runtimeWorker);

            var result = await coordinator.RunOnceAsync(
                helperRuntimeInstanceId: "runtime-helper-1",
                helperIsIdle: true,
                helperQueueDepth: 0,
                helperAvailableWorkerSlots: 1);

            Assert.True(result.Enabled);
            Assert.Equal(1, result.CandidateCount);
            Assert.Equal(1, result.EvaluatedDecisionCount);
            Assert.Equal(1, result.GrantedLeaseCount);
            Assert.Equal(1, result.StartedPumpCount);
            Assert.Single(result.Decisions);
            Assert.Single(result.PumpResults);
            Assert.Equal(1, runtimeWorker.RunCount);
        }

        /// <summary>
        /// Validates that the coordinator does not start the pump when the candidate
        /// does not meet assistance thresholds.
        /// </summary>
        [Fact]
        public async Task RunOnceAsync_Should_Not_Start_Pump_When_Candidate_Is_Not_Eligible()
        {
            var candidateStore = new InMemoryAiExecutionAssistanceCandidateStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();

            await candidateStore.UpsertAsync(
                CreateCandidate(
                    estimatedReadyStepCount: 1,
                    estimatedRemainingStepCount: 5,
                    estimatedActiveWorkerCount: 1));

            var coordinator = CreateCoordinator(
                candidateStore: candidateStore,
                runtimeWorker: runtimeWorker);

            var result = await coordinator.RunOnceAsync(
                helperRuntimeInstanceId: "runtime-helper-1",
                helperIsIdle: true,
                helperQueueDepth: 0,
                helperAvailableWorkerSlots: 1);

            Assert.True(result.Enabled);
            Assert.Equal(1, result.CandidateCount);
            Assert.Equal(1, result.EvaluatedDecisionCount);
            Assert.Equal(0, result.GrantedLeaseCount);
            Assert.Equal(0, result.StartedPumpCount);
            Assert.Single(result.Decisions);
            Assert.Empty(result.PumpResults);
            Assert.Equal(0, runtimeWorker.RunCount);
        }

        private static AiExecutionAssistanceCoordinator CreateCoordinator(
            IAiExecutionAssistanceCandidateStore? candidateStore = null,
            IAiExecutionAssistanceStore? assistanceStore = null,
            IAiRuntimeInstanceWorker? runtimeWorker = null,
            Action<AiExecutionAssistanceOptions>? configureOptions = null)
        {
            var options = new AiExecutionAssistanceOptions
            {
                Enabled = true,
                MaxHelpersPerExecution = 1,
                MaxWorkersPerExecution = 4,
                MaxWorkersPerHelperInstance = 1,
                MinReadyStepsToAssist = 10,
                MinRemainingStepsToAssist = 100,
                OnlyWhenLocalQueueIdle = true,
                MaxHelperQueueDepth = 0,
                LeaseTtl = TimeSpan.FromSeconds(30),
                EvaluationInterval = TimeSpan.FromSeconds(2)
            };

            configureOptions?.Invoke(options);

            var resolvedCandidateStore =
                candidateStore ?? new InMemoryAiExecutionAssistanceCandidateStore();

            var resolvedAssistanceStore =
                assistanceStore ?? new InMemoryAiExecutionAssistanceStore();

            var resolvedRuntimeWorker =
                runtimeWorker ?? new FakeRuntimeInstanceWorker();

            var controller = new AiExecutionAssistanceController(
                resolvedAssistanceStore,
                Options.Create(options));

            var worker = new AiExecutionAssistanceWorker(
                resolvedAssistanceStore,
                resolvedRuntimeWorker);

            var pump = new AiExecutionAssistancePump(
                worker,
                resolvedAssistanceStore);

            return new AiExecutionAssistanceCoordinator(
                resolvedCandidateStore,
                resolvedAssistanceStore,
                controller,
                pump,
                Options.Create(options));
        }

        private static AiExecutionAssistanceCandidate CreateCandidate(
            string executionId = "execution-1",
            string primaryRuntimeInstanceId = "runtime-primary-1",
            string pipelineName = "pipeline-1",
            int estimatedReadyStepCount = 50,
            int estimatedRemainingStepCount = 200,
            int estimatedActiveWorkerCount = 1)
        {
            return new AiExecutionAssistanceCandidate
            {
                ExecutionId = executionId,
                PrimaryRuntimeInstanceId = primaryRuntimeInstanceId,
                LocalRunId = "local-run-1",
                PipelineName = pipelineName,
                PipelineVersion = "1.0.0",
                EstimatedReadyStepCount = estimatedReadyStepCount,
                EstimatedRemainingStepCount = estimatedRemainingStepCount,
                EstimatedActiveWorkerCount = estimatedActiveWorkerCount,
                Metadata = new Dictionary<string, string>
                {
                    ["test"] = "true"
                }
            };
        }

        private sealed class FakeRuntimeInstanceWorker : IAiRuntimeInstanceWorker
        {
            public int RunCount { get; private set; }

            public Task<AiExecutionRecord> RunExecutionAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

                cancellationToken.ThrowIfCancellationRequested();

                RunCount++;

                return Task.FromResult(
                    new AiExecutionRecord
                    {
                        ExecutionId = executionId,
                        Status = AiExecutionStatus.Completed
                    });
            }
        }
    }
}