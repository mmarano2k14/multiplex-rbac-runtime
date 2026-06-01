using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;
using Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Unit tests for <see cref="InMemoryAiExecutionAssistanceCandidateStore"/>.
    /// </summary>
    public sealed class AiExecutionAssistanceCandidateStoreTests
    {
        /// <summary>
        /// Validates that a candidate can be inserted and retrieved by execution identifier.
        /// </summary>
        [Fact]
        public async Task UpsertAsync_Should_Insert_And_Get_Candidate()
        {
            var store = new InMemoryAiExecutionAssistanceCandidateStore();

            var candidate = CreateCandidate();

            await store.UpsertAsync(candidate);

            var stored = await store.GetAsync(candidate.ExecutionId);

            Assert.NotNull(stored);
            Assert.Equal(candidate.ExecutionId, stored!.ExecutionId);
            Assert.Equal(candidate.PrimaryRuntimeInstanceId, stored.PrimaryRuntimeInstanceId);
            Assert.Equal(candidate.PipelineName, stored.PipelineName);
            Assert.True(stored.IsActive);
        }

        /// <summary>
        /// Validates that an existing candidate can be updated while preserving its registration time.
        /// </summary>
        [Fact]
        public async Task UpsertAsync_Should_Update_Existing_Candidate()
        {
            var store = new InMemoryAiExecutionAssistanceCandidateStore();

            var candidate = CreateCandidate(
                estimatedReadyStepCount: 10,
                estimatedRemainingStepCount: 100,
                estimatedActiveWorkerCount: 1);

            await store.UpsertAsync(candidate);

            var first = await store.GetAsync(candidate.ExecutionId);

            Assert.NotNull(first);

            var updated = CreateCandidate(
                estimatedReadyStepCount: 25,
                estimatedRemainingStepCount: 75,
                estimatedActiveWorkerCount: 2,
                metadata: new Dictionary<string, string>
                {
                    ["updated"] = "true"
                });

            await store.UpsertAsync(updated);

            var stored = await store.GetAsync(candidate.ExecutionId);

            Assert.NotNull(stored);
            Assert.Equal(25, stored!.EstimatedReadyStepCount);
            Assert.Equal(75, stored.EstimatedRemainingStepCount);
            Assert.Equal(2, stored.EstimatedActiveWorkerCount);
            Assert.Equal(first!.RegisteredAtUtc, stored.RegisteredAtUtc);
            Assert.True(stored.Metadata.ContainsKey("updated"));
        }

        /// <summary>
        /// Validates that only active candidates are returned by default.
        /// </summary>
        [Fact]
        public async Task ListActiveAsync_Should_Return_Only_Active_Candidates()
        {
            var store = new InMemoryAiExecutionAssistanceCandidateStore();

            var active = CreateCandidate(
                executionId: "execution-active");

            var completed = CreateCandidate(
                executionId: "execution-completed");

            await store.UpsertAsync(active);
            await store.UpsertAsync(completed);

            await store.MarkCompletedAsync(
                completed.ExecutionId,
                "completed by test");

            var activeCandidates = await store.ListActiveAsync();

            Assert.Single(activeCandidates);
            Assert.Equal(active.ExecutionId, activeCandidates.Single().ExecutionId);
        }

        /// <summary>
        /// Validates that marking a candidate as completed deactivates it.
        /// </summary>
        [Fact]
        public async Task MarkCompletedAsync_Should_Deactivate_Candidate()
        {
            var store = new InMemoryAiExecutionAssistanceCandidateStore();

            var candidate = CreateCandidate();

            await store.UpsertAsync(candidate);

            await store.MarkCompletedAsync(
                candidate.ExecutionId,
                "completed by unit test");

            var stored = await store.GetAsync(candidate.ExecutionId);

            Assert.NotNull(stored);
            Assert.False(stored!.IsActive);
            Assert.NotNull(stored.CompletedAtUtc);
            Assert.Equal("completed by unit test", stored.Reason);

            var activeCandidates = await store.ListActiveAsync();

            Assert.Empty(activeCandidates);
        }

        private static AiExecutionAssistanceCandidate CreateCandidate(
            string executionId = "execution-1",
            string primaryRuntimeInstanceId = "runtime-instance-1",
            string? localRunId = "local-run-1",
            string pipelineName = "pipeline-1",
            string? pipelineVersion = "1.0.0",
            int estimatedReadyStepCount = 50,
            int estimatedRemainingStepCount = 100,
            int estimatedActiveWorkerCount = 1,
            bool isActive = true,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            return new AiExecutionAssistanceCandidate
            {
                ExecutionId = executionId,
                PrimaryRuntimeInstanceId = primaryRuntimeInstanceId,
                LocalRunId = localRunId,
                PipelineName = pipelineName,
                PipelineVersion = pipelineVersion,
                EstimatedReadyStepCount = estimatedReadyStepCount,
                EstimatedRemainingStepCount = estimatedRemainingStepCount,
                EstimatedActiveWorkerCount = estimatedActiveWorkerCount,
                IsActive = isActive,
                RegisteredAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Metadata = metadata ?? new Dictionary<string, string>
                {
                    ["test"] = "true"
                }
            };
        }
    }
}