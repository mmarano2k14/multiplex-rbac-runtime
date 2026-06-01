using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Unit tests for <see cref="AiExecutionAssistanceWorker"/>.
    /// </summary>
    public sealed class AiExecutionAssistanceWorkerTests
    {
        /// <summary>
        /// Validates that the assistance worker advances an existing execution
        /// through the runtime instance worker and releases the lease.
        /// </summary>
        [Fact]
        public async Task AssistAsync_Should_Run_Existing_Execution_And_Release_Lease()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();

            var worker = new AiExecutionAssistanceWorker(
                store,
                runtimeWorker);

            var lease = CreateLease();

            await store.RegisterAsync(lease);

            await worker.AssistAsync(lease);

            Assert.Equal(1, runtimeWorker.RunCount);
            Assert.Equal(lease.ExecutionId, runtimeWorker.LastExecutionId);

            var storedLease = await store.GetAsync(lease.LeaseId);

            Assert.NotNull(storedLease);
            Assert.Equal(AiExecutionAssistanceStatus.Released, storedLease!.Status);
            Assert.NotNull(storedLease.StartedAtUtc);
            Assert.NotNull(storedLease.CompletedAtUtc);
            Assert.Contains(
                "Execution assistance worker completed with execution status",
                storedLease.Reason,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Validates that the assistance worker marks the lease as failed when
        /// the runtime worker fails.
        /// </summary>
        [Fact]
        public async Task AssistAsync_Should_Mark_Lease_Failed_When_Runtime_Worker_Fails()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker
            {
                Failure = new InvalidOperationException("runtime worker failure")
            };

            var worker = new AiExecutionAssistanceWorker(
                store,
                runtimeWorker);

            var lease = CreateLease();

            await store.RegisterAsync(lease);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => worker.AssistAsync(lease));

            Assert.Equal("runtime worker failure", exception.Message);

            var storedLease = await store.GetAsync(lease.LeaseId);

            Assert.NotNull(storedLease);
            Assert.Equal(AiExecutionAssistanceStatus.Failed, storedLease!.Status);
            Assert.Equal("runtime worker failure", storedLease.Reason);
            Assert.NotNull(storedLease.CompletedAtUtc);
        }

        /// <summary>
        /// Validates that the assistance worker marks the lease as expired when
        /// the lease is already expired before work starts.
        /// </summary>
        [Fact]
        public async Task AssistAsync_Should_Mark_Lease_Expired_When_Lease_Is_Expired()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();

            var worker = new AiExecutionAssistanceWorker(
                store,
                runtimeWorker);

            var lease = CreateLease(
                expiresAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1));

            await store.RegisterAsync(lease);

            await worker.AssistAsync(lease);

            Assert.Equal(0, runtimeWorker.RunCount);

            var storedLease = await store.GetAsync(lease.LeaseId);

            Assert.NotNull(storedLease);
            Assert.Equal(AiExecutionAssistanceStatus.Expired, storedLease!.Status);
            Assert.NotNull(storedLease.CompletedAtUtc);
        }

        /// <summary>
        /// Validates that terminal leases cannot be used for assistance.
        /// </summary>
        [Fact]
        public async Task AssistAsync_Should_Throw_When_Lease_Is_Terminal()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();

            var worker = new AiExecutionAssistanceWorker(
                store,
                runtimeWorker);

            var lease = CreateLease(
                status: AiExecutionAssistanceStatus.Released);

            await store.RegisterAsync(lease);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => worker.AssistAsync(lease));

            Assert.Contains(
                $"Execution assistance lease '{lease.LeaseId}' cannot be used because its status is 'Released'.",
                exception.Message,
                StringComparison.Ordinal);

            Assert.Equal(0, runtimeWorker.RunCount);
        }

        private static AiExecutionAssistanceLease CreateLease(
            string leaseId = "lease-1",
            string executionId = "execution-1",
            string primaryRuntimeInstanceId = "runtime-primary-1",
            string helperRuntimeInstanceId = "runtime-helper-1",
            int maxWorkers = 1,
            AiExecutionAssistanceStatus status = AiExecutionAssistanceStatus.Granted,
            DateTimeOffset? expiresAtUtc = null)
        {
            return new AiExecutionAssistanceLease
            {
                LeaseId = leaseId,
                ExecutionId = executionId,
                PrimaryRuntimeInstanceId = primaryRuntimeInstanceId,
                HelperRuntimeInstanceId = helperRuntimeInstanceId,
                MaxWorkers = maxWorkers,
                Status = status,
                GrantedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = expiresAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(1),
                Reason = "Unit test lease.",
                Metadata = new Dictionary<string, string>
                {
                    ["test"] = "true"
                }
            };
        }

        private sealed class FakeRuntimeInstanceWorker : IAiRuntimeInstanceWorker
        {
            public int RunCount { get; private set; }

            public string? LastExecutionId { get; private set; }

            public Exception? Failure { get; init; }

            public Task<AiExecutionRecord> RunExecutionAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

                cancellationToken.ThrowIfCancellationRequested();

                if (Failure is not null)
                {
                    throw Failure;
                }

                RunCount++;
                LastExecutionId = executionId;

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