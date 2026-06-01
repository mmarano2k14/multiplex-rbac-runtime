using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance;
using Multiplexed.AI.Tests.Runtime.Execution.MultiInstance;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Unit tests for <see cref="AiExecutionAssistancePump"/>.
    /// </summary>
    public sealed class AiExecutionAssistancePumpTests
    {
        /// <summary>
        /// Validates that the pump starts the expected number of helper workers
        /// and releases the assistance lease after lifecycle execution.
        /// </summary>
        [Fact]
        public async Task PumpAsync_Should_Start_Helper_Workers_And_Release_Lease()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();
            var worker = new AiExecutionAssistanceWorker(store, runtimeWorker);
            var pump = new AiExecutionAssistancePump(worker, store);

            var lease = CreateLease(
                maxWorkers: 3);

            await store.RegisterAsync(lease);

            var result = await pump.PumpAsync(lease);

            Assert.True(result.Success);
            Assert.Equal(lease.LeaseId, result.LeaseId);
            Assert.Equal(lease.ExecutionId, result.ExecutionId);
            Assert.Equal(lease.HelperRuntimeInstanceId, result.HelperRuntimeInstanceId);
            Assert.Equal(3, result.StartedWorkerCount);
            Assert.Equal(3, runtimeWorker.RunCount);

            var storedLease = await store.GetAsync(lease.LeaseId);

            Assert.NotNull(storedLease);
            Assert.Equal(AiExecutionAssistanceStatus.Released, storedLease!.Status);
            Assert.NotNull(storedLease.StartedAtUtc);
            Assert.NotNull(storedLease.CompletedAtUtc);
        }

        /// <summary>
        /// Validates that the pump fails when a lease does not allow any helper workers.
        /// </summary>
        [Fact]
        public async Task PumpAsync_Should_Fail_When_Lease_Has_No_Worker_Budget()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();
            var worker = new AiExecutionAssistanceWorker(store, runtimeWorker);
            var pump = new AiExecutionAssistancePump(worker, store);

            var lease = CreateLease(
                maxWorkers: 0);

            await store.RegisterAsync(lease);

            var result = await pump.PumpAsync(lease);

            Assert.False(result.Success);
            Assert.Equal(lease.LeaseId, result.LeaseId);
            Assert.Equal(0, result.StartedWorkerCount);
            Assert.Equal(0, runtimeWorker.RunCount);
            Assert.Equal("Execution assistance lease does not allow any helper workers.", result.FailureReason);
        }

        /// <summary>
        /// Validates that an expired lease is marked as expired and no helper workers are started.
        /// </summary>
        [Fact]
        public async Task PumpAsync_Should_Expire_Lease_When_Lease_Is_Expired()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();
            var worker = new AiExecutionAssistanceWorker(store, runtimeWorker);
            var pump = new AiExecutionAssistancePump(worker, store);

            var lease = CreateLease(
                maxWorkers: 2,
                expiresAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1));

            await store.RegisterAsync(lease);

            var result = await pump.PumpAsync(lease);

            Assert.False(result.Success);
            Assert.Equal(AiExecutionAssistanceStatus.Expired, result.Status);
            Assert.Equal(0, result.StartedWorkerCount);
            Assert.Equal(0, runtimeWorker.RunCount);
            Assert.Equal("Execution assistance lease expired before pump started.", result.FailureReason);

            var storedLease = await store.GetAsync(lease.LeaseId);

            Assert.NotNull(storedLease);
            Assert.Equal(AiExecutionAssistanceStatus.Expired, storedLease!.Status);
            Assert.NotNull(storedLease.CompletedAtUtc);
        }

        /// <summary>
        /// Validates that the assistance worker marks an expired lease as expired
        /// when it is called directly.
        /// </summary>
        [Fact]
        public async Task AssistAsync_Should_Expire_Lease_When_Lease_Expired_Before_Work_Start()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();
            var worker = new AiExecutionAssistanceWorker(store, runtimeWorker);

            var lease = CreateLease(
                maxWorkers: 1,
                expiresAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1));

            await store.RegisterAsync(lease);

            await worker.AssistAsync(lease);

            var storedLease = await store.GetAsync(lease.LeaseId);

            Assert.NotNull(storedLease);
            Assert.Equal(AiExecutionAssistanceStatus.Expired, storedLease!.Status);
            Assert.Equal(0, runtimeWorker.RunCount);
        }

        /// <summary>
        /// Validates that the assistance worker rejects leases that are already terminal.
        /// </summary>
        [Fact]
        public async Task AssistAsync_Should_Throw_When_Lease_Is_Terminal()
        {
            var store = new InMemoryAiExecutionAssistanceStore();
            var runtimeWorker = new FakeRuntimeInstanceWorker();
            var worker = new AiExecutionAssistanceWorker(store, runtimeWorker);

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

        /// <summary>
        /// Validates that the in-memory store can list active leases by execution.
        /// </summary>
        [Fact]
        public async Task Store_Should_List_Active_Leases_By_Execution()
        {
            var store = new InMemoryAiExecutionAssistanceStore();

            var lease1 = CreateLease(
                leaseId: "lease-1",
                executionId: "execution-1",
                helperRuntimeInstanceId: "runtime-helper-1");

            var lease2 = CreateLease(
                leaseId: "lease-2",
                executionId: "execution-1",
                helperRuntimeInstanceId: "runtime-helper-2");

            var lease3 = CreateLease(
                leaseId: "lease-3",
                executionId: "execution-2",
                helperRuntimeInstanceId: "runtime-helper-3");

            await store.RegisterAsync(lease1);
            await store.RegisterAsync(lease2);
            await store.RegisterAsync(lease3);

            var executionLeases = await store.ListByExecutionAsync(
                "execution-1");

            Assert.Equal(2, executionLeases.Count);
            Assert.Contains(executionLeases, lease => lease.LeaseId == "lease-1");
            Assert.Contains(executionLeases, lease => lease.LeaseId == "lease-2");
        }

        /// <summary>
        /// Validates that the in-memory store excludes terminal leases by default.
        /// </summary>
        [Fact]
        public async Task Store_Should_Exclude_Terminal_Leases_By_Default()
        {
            var store = new InMemoryAiExecutionAssistanceStore();

            var activeLease = CreateLease(
                leaseId: "active-lease",
                executionId: "execution-1",
                status: AiExecutionAssistanceStatus.Active);

            var releasedLease = CreateLease(
                leaseId: "released-lease",
                executionId: "execution-1",
                status: AiExecutionAssistanceStatus.Released);

            await store.RegisterAsync(activeLease);
            await store.RegisterAsync(releasedLease);

            var nonTerminalLeases = await store.ListByExecutionAsync(
                "execution-1");

            Assert.Single(nonTerminalLeases);
            Assert.Equal("active-lease", nonTerminalLeases.Single().LeaseId);

            var allLeases = await store.ListByExecutionAsync(
                "execution-1",
                includeTerminal: true);

            Assert.Equal(2, allLeases.Count);
        }

        /// <summary>
        /// Validates that the in-memory store can list active leases by helper runtime instance.
        /// </summary>
        [Fact]
        public async Task Store_Should_List_Active_Leases_By_Helper()
        {
            var store = new InMemoryAiExecutionAssistanceStore();

            var lease1 = CreateLease(
                leaseId: "lease-1",
                executionId: "execution-1",
                helperRuntimeInstanceId: "runtime-helper-1");

            var lease2 = CreateLease(
                leaseId: "lease-2",
                executionId: "execution-2",
                helperRuntimeInstanceId: "runtime-helper-1");

            var lease3 = CreateLease(
                leaseId: "lease-3",
                executionId: "execution-3",
                helperRuntimeInstanceId: "runtime-helper-2");

            await store.RegisterAsync(lease1);
            await store.RegisterAsync(lease2);
            await store.RegisterAsync(lease3);

            var helperLeases = await store.ListByHelperAsync(
                "runtime-helper-1");

            Assert.Equal(2, helperLeases.Count);
            Assert.Contains(helperLeases, lease => lease.LeaseId == "lease-1");
            Assert.Contains(helperLeases, lease => lease.LeaseId == "lease-2");
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
    }
}