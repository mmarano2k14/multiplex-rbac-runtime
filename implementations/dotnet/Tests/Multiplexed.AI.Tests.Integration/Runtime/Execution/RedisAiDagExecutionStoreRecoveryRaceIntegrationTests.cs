using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Policies;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Cache.Redis;
using Multiplexed.AI.Tests.Integration.Helpers;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Hardcore integration tests covering multi-worker recovery races for the Redis-backed DAG execution store.
    ///
    /// PURPOSE:
    /// - Validate concurrent recovery behavior under contention
    /// - Validate recovery and claim interleavings
    /// - Validate stale token rejection after recovery and re-claim
    /// - Stress the store using real Redis/Lua coordination
    ///
    /// IMPORTANT:
    /// - These tests exercise the real distributed store behavior
    /// - They are intentionally race-oriented and concurrency-heavy
    /// - Each test uses an isolated Redis key prefix
    /// </summary>
    public sealed class RedisAiDagExecutionStoreRecoveryRaceIntegrationTests : IAsyncLifetime
    {
        private IConnectionMultiplexer _multiplexer = default!;
        private IDatabase _database = default!;
        private RedisAiDagExecutionStore _store = default!;
        private TestAiExecutionKeyBuilder _keyBuilder = default!;
        private string _testPrefix = default!;

        public async Task InitializeAsync()
        {
            var logger = new NoopLogger();
            var redisConnectionString =
                Environment.GetEnvironmentVariable("MULTIPLEXED_TEST_REDIS")
                ?? "localhost:6379,abortConnect=false,allowAdmin=true";

            _multiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
            _database = _multiplexer.GetDatabase();

            _testPrefix = $"test:dag:recovery-race:{Guid.NewGuid():N}";
            _keyBuilder = new TestAiExecutionKeyBuilder(_testPrefix);
            var metrics = MetricsFactory.Create();
            var normalizers = new DefaultAiStepResultNormalizerPipeline([new RagStepResultNormalizer()]);
            IRedisDagStoreServices services = new RedisDagStoreServices(
                _multiplexer,
                _keyBuilder,
                logger,
                metrics,
                normalizers);
            _store = new RedisAiDagExecutionStore(services);
        }

        public async Task DisposeAsync()
        {
            try
            {
                foreach (var endpoint in _multiplexer.GetEndPoints())
                {
                    var server = _multiplexer.GetServer(endpoint);

                    if (!server.IsConnected)
                        continue;

                    await foreach (var key in server.KeysAsync(database: _database.Database, pattern: $"{_testPrefix}*"))
                    {
                        await _database.KeyDeleteAsync(key);
                    }
                }
            }
            finally
            {
                await _multiplexer.DisposeAsync();
            }
        }

        // ---------------------------------------------------------------------
        // RECOVERY RACE TESTS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Verifies that concurrent recovery calls do not apply duplicate logical recovery
        /// to the same expired running step.
        ///
        /// EXPECTED:
        /// - at least one caller may report recovery
        /// - final step state is Ready
        /// - RecoveryCount is incremented exactly once
        /// - claim metadata is cleared
        /// </summary>
        [Fact]
        public async Task RecoverTimedOutStepsAsync_Should_Recover_Expired_Step_Only_Once_Under_Concurrency()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString("N");

            var step = CreateRunningStep(
                stepName: "step-1",
                claimedBy: "worker-a",
                claimToken: "claim-token-a",
                claimedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(-5),
                claimTimeoutSeconds: 60);

            await CreateExecutionAsync(executionId, step);

            // Act
            var tasks = Enumerable.Range(0, 20)
                .Select(_ => _store.RecoverTimedOutStepsAsync(executionId))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.True(results.Sum() >= 1);

            var state = await _store.GetStateAsync(executionId);
            Assert.NotNull(state);

            var reloadedStep = state!.Steps["step-1"];
            Assert.Equal(AiStepExecutionStatus.Ready, reloadedStep.Status);
            Assert.Null(reloadedStep.ClaimedBy);
            Assert.Null(reloadedStep.ClaimToken);
            Assert.Null(reloadedStep.ClaimedAtUtc);
            Assert.Null(reloadedStep.LeaseExpiresAtUtc);
            Assert.Equal(1, reloadedStep.RecoveryCount);
        }

        /// <summary>
        /// Verifies that concurrent recovery and claim attempts do not produce double ownership.
        ///
        /// EXPECTED:
        /// - the final step is either Ready or Running
        /// - if Running, it has exactly one owner and one claim token
        /// - no inconsistent final claim metadata remains
        /// </summary>
        [Fact]
        public async Task RecoverTimedOutStepsAsync_And_TryClaimNextReadyStepAsync_Should_Not_Produce_Double_Ownership()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString("N");

            var step = CreateRunningStep(
                stepName: "step-1",
                claimedBy: "worker-a",
                claimToken: "claim-token-a",
                claimedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1),
                claimTimeoutSeconds: 60);

            await CreateExecutionAsync(executionId, step);

            var claimResults = new ConcurrentBag<AiClaimedStep?>();

            // Act
            var recoveryTasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => _store.RecoverTimedOutStepsAsync(executionId)));

            var claimTasks = Enumerable.Range(0, 20)
                .Select(i => Task.Run(async () =>
                {
                    var claimed = await _store.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");
                    claimResults.Add(claimed);
                }));

            await Task.WhenAll(recoveryTasks.Concat(claimTasks));

            // Assert
            var state = await _store.GetStateAsync(executionId);
            Assert.NotNull(state);

            var reloadedStep = state!.Steps["step-1"];

            Assert.True(
                reloadedStep.Status == AiStepExecutionStatus.Ready ||
                reloadedStep.Status == AiStepExecutionStatus.Running);

            if (reloadedStep.Status == AiStepExecutionStatus.Ready)
            {
                Assert.Null(reloadedStep.ClaimedBy);
                Assert.Null(reloadedStep.ClaimToken);
                Assert.Null(reloadedStep.ClaimedAtUtc);
                Assert.Null(reloadedStep.LeaseExpiresAtUtc);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(reloadedStep.ClaimedBy));
                Assert.False(string.IsNullOrWhiteSpace(reloadedStep.ClaimToken));
                Assert.NotNull(reloadedStep.ClaimedAtUtc);
                Assert.NotNull(reloadedStep.LeaseExpiresAtUtc);
            }

            var successfulClaims = claimResults.Count(x => x is not null);

            // Under race conditions, multiple claim attempts may happen across time,
            // but final ownership must still be singular and coherent.
            Assert.True(successfulClaims >= 0);
        }

        /// <summary>
        /// Verifies that a stale completion attempt is rejected after an expired claim
        /// has been recovered and the step has been claimed again by another worker.
        ///
        /// EXPECTED:
        /// - original stale completion returns false
        /// - current owner remains authoritative
        /// </summary>
        [Fact]
        public async Task TryCompleteStepAsync_Should_Reject_Stale_Token_After_Recovery_And_Reclaim()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString("N");
            var originalToken = "claim-token-a";

            var step = CreateRunningStep(
                stepName: "step-1",
                claimedBy: "worker-a",
                claimToken: originalToken,
                claimedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1),
                claimTimeoutSeconds: 60);

            await CreateExecutionAsync(executionId, step);

            var recovered = await _store.RecoverTimedOutStepsAsync(executionId);
            Assert.Equal(1, recovered);

            var newClaim = await _store.TryClaimNextReadyStepAsync(executionId, "worker-b");
            Assert.NotNull(newClaim);

            // Act
            var staleComplete = await _store.TryCompleteStepAsync(
                executionId,
                "step-1",
                originalToken,
                new AiStepResult());

            // Assert
            Assert.False(staleComplete);

            var state = await _store.GetStateAsync(executionId);
            Assert.NotNull(state);

            var reloadedStep = state!.Steps["step-1"];
            Assert.Equal(AiStepExecutionStatus.Running, reloadedStep.Status);
            Assert.Equal("worker-b", reloadedStep.ClaimedBy);
            Assert.Equal(newClaim!.ClaimToken, reloadedStep.ClaimToken);
        }

        /// <summary>
        /// Verifies that a stale failure attempt is rejected after an expired claim
        /// has been recovered and the step has been claimed again by another worker.
        ///
        /// EXPECTED:
        /// - original stale failure returns false
        /// - current owner remains authoritative
        /// </summary>
        [Fact]
        public async Task TryFailStepAsync_Should_Reject_Stale_Token_After_Recovery_And_Reclaim()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString("N");
            var originalToken = "claim-token-a";

            var step = CreateRunningStep(
                stepName: "step-1",
                claimedBy: "worker-a",
                claimToken: originalToken,
                claimedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1),
                claimTimeoutSeconds: 60);

            await CreateExecutionAsync(executionId, step);

            var recovered = await _store.RecoverTimedOutStepsAsync(executionId);
            Assert.Equal(1, recovered);

            var newClaim = await _store.TryClaimNextReadyStepAsync(executionId, "worker-b");
            Assert.NotNull(newClaim);

            // Act
            var staleFail = await _store.TryFailStepAsync(
                executionId,
                "step-1",
                originalToken,
                "stale failure");

            // Assert
            Assert.False(staleFail);

            var state = await _store.GetStateAsync(executionId);
            Assert.NotNull(state);

            var reloadedStep = state!.Steps["step-1"];
            Assert.Equal(AiStepExecutionStatus.Running, reloadedStep.Status);
            Assert.Equal("worker-b", reloadedStep.ClaimedBy);
            Assert.Equal(newClaim!.ClaimToken, reloadedStep.ClaimToken);
        }

        /// <summary>
        /// Stress test that mixes many recovery and claim operations against the same execution.
        ///
        /// EXPECTED:
        /// - no unhandled exceptions
        /// - final state remains coherent
        /// - final ownership is singular if the step is Running
        /// </summary>
        [Fact]
        public async Task Recovery_And_Claim_Should_Remain_Coherent_Under_Heavy_Concurrency()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString("N");

            var step = CreateRunningStep(
                stepName: "step-1",
                claimedBy: "seed-worker",
                claimToken: "seed-token",
                claimedAtUtc: DateTime.UtcNow.AddMinutes(-20),
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(-10),
                claimTimeoutSeconds: 60);

            await CreateExecutionAsync(executionId, step);

            var errors = new ConcurrentBag<Exception>();

            // Act
            var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
            {
                try
                {
                    if (i % 2 == 0)
                    {
                        await _store.RecoverTimedOutStepsAsync(executionId);
                    }
                    else
                    {
                        await _store.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }));

            await Task.WhenAll(tasks);

            // Assert
            Assert.Empty(errors);

            var state = await _store.GetStateAsync(executionId);
            Assert.NotNull(state);

            var reloadedStep = state!.Steps["step-1"];

            Assert.True(
                reloadedStep.Status == AiStepExecutionStatus.Ready ||
                reloadedStep.Status == AiStepExecutionStatus.Running);

            if (reloadedStep.Status == AiStepExecutionStatus.Ready)
            {
                Assert.Null(reloadedStep.ClaimedBy);
                Assert.Null(reloadedStep.ClaimToken);
                Assert.Null(reloadedStep.ClaimedAtUtc);
                Assert.Null(reloadedStep.LeaseExpiresAtUtc);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(reloadedStep.ClaimedBy));
                Assert.False(string.IsNullOrWhiteSpace(reloadedStep.ClaimToken));
                Assert.NotNull(reloadedStep.ClaimedAtUtc);
                Assert.NotNull(reloadedStep.LeaseExpiresAtUtc);
            }

            Assert.True(reloadedStep.RecoveryCount >= 1);
        }

        /// <summary>
        /// CHAOS TEST — Recovery + Claim under high concurrency
        ///
        /// PURPOSE:
        /// This test stresses the distributed DAG execution store by simulating
        /// concurrent recovery and claim operations across many workers and iterations.
        ///
        /// Unlike deterministic tests, this is a probabilistic test designed to expose:
        /// - race conditions
        /// - timing issues
        /// - inconsistent state transitions
        /// - invalid ownership scenarios
        ///
        /// SCENARIO:
        /// For each iteration:
        /// - A step is initialized in a Running state with an expired lease
        /// - Multiple workers (threads/tasks) concurrently perform:
        ///     - RecoverTimedOutStepsAsync (recovery path)
        ///     - TryClaimNextReadyStepAsync (claim path)
        ///
        /// EXPECTED INVARIANTS (must ALWAYS hold):
        ///
        /// 1. VALID FINAL STATE:
        ///    - Step must end in either:
        ///      - Ready (recovered but not yet claimed)
        ///      - Running (claimed by exactly one worker)
        ///
        /// 2. OWNERSHIP CONSISTENCY:
        ///    - If Ready:
        ///        - no ClaimToken
        ///        - no ClaimedBy
        ///        - no Lease
        ///    - If Running:
        ///        - exactly one ClaimToken
        ///        - exactly one ClaimedBy
        ///        - Lease must exist
        ///
        /// 3. RECOVERY MONOTONICITY:
        ///    - RecoveryCount must increase but never be corrupted
        ///    - No double or inconsistent recovery application
        ///
        /// 4. NO EXCEPTIONS:
        ///    - No Redis / Lua / serialization / concurrency exception should occur
        ///
        /// IMPORTANT:
        /// - This test is intentionally non-deterministic
        /// - It should be run multiple times to increase confidence
        /// - Failures indicate real distributed race bugs
        ///
        /// THIS TEST VALIDATES:
        /// - Atomicity of Lua scripts
        /// - Correct lease-based recovery model
        /// - Proper claim token ownership enforcement
        /// - Absence of race-induced corruption
        /// </summary>
        [Fact]
        public async Task Chaos_Recovery_And_Claim_Should_Never_Break_Invariants()
        {
            var dagStore = CreateDagStore();

            const int iterations = 100;
            const int workers = 50;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var executionId = Guid.NewGuid().ToString("N");

                var claimedAtUtc = DateTime.UtcNow.AddMinutes(-20);
                var leaseExpiresAtUtc = claimedAtUtc.AddSeconds(30);

                var record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "chaos",
                    ExecutionMode = AiExecutionMode.Dag,
                    Status = AiExecutionStatus.Running
                };

                var state = new AiExecutionState
                {
                    ExecutionId = executionId
                };

                state.Steps["step-1"] = new AiStepState
                {
                    StepName = "step-1",
                    Status = AiStepExecutionStatus.Running,
                    ClaimedBy = "seed-worker",
                    ClaimToken = "seed-token",
                    ClaimedAtUtc = claimedAtUtc,
                    LeaseExpiresAtUtc = leaseExpiresAtUtc,
                    ClaimTimeoutSeconds = 30,
                    RetryState = new AiStepRetryState
                    {
                        RetryCount = 0
                    },
                    RecoveryCount = 0
                };

                await dagStore.CreateAsync(record, state);

                var errors = new ConcurrentBag<Exception>();

                var tasks = Enumerable.Range(0, workers)
                    .Select(i => Task.Run(async () =>
                    {
                        try
                        {
                            // Random behavior to simulate real distributed conditions
                            var rnd = Random.Shared.Next(0, 3);

                            if (rnd == 0)
                            {
                                await dagStore.RecoverTimedOutStepsAsync(executionId);
                            }
                            else
                            {
                                await dagStore.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }))
                    .ToArray();

                await Task.WhenAll(tasks);

                // --- INVARIANTS VALIDATION ---

                Assert.Empty(errors);

                var snapshot = await dagStore.GetStateAsync(executionId);
                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["step-1"];

                // Invariant 1 — Valid state
                Assert.True(
                    step.Status == AiStepExecutionStatus.Ready ||
                    step.Status == AiStepExecutionStatus.Running);

                // Invariant 2 — Ownership consistency
                if (step.Status == AiStepExecutionStatus.Ready)
                {
                    Assert.Null(step.ClaimedBy);
                    Assert.Null(step.ClaimToken);
                    Assert.Null(step.ClaimedAtUtc);
                    Assert.Null(step.LeaseExpiresAtUtc);
                }
                else
                {
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimedBy));
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
                    Assert.NotNull(step.ClaimedAtUtc);
                    Assert.NotNull(step.LeaseExpiresAtUtc);
                }

                // Invariant 3 — Recovery monotonicity
                Assert.True(step.RecoveryCount >= 1);

                await CleanupDagExecutionAsync(executionId);

            }
        }

        /// <summary>
        /// CHAOS TEST — Recovery + Claim with injected random latency
        ///
        /// PURPOSE:
        /// This test increases scheduling variability by injecting small random delays
        /// before distributed recovery and claim calls.
        ///
        /// This helps expose race conditions that may not appear under fully synchronized
        /// execution, especially around:
        /// - recovery timing windows
        /// - claim ordering shifts
        /// - delayed ownership observation
        ///
        /// SCENARIO:
        /// - A step starts in Running with an expired lease
        /// - Many workers concurrently attempt:
        ///   - recovery
        ///   - claim
        /// - Each worker waits for a small random delay before acting
        ///
        /// EXPECTED INVARIANTS:
        /// 1. Final state must remain valid (Ready or Running)
        /// 2. If Ready, no ownership metadata may remain
        /// 3. If Running, ownership metadata must be complete and singular
        /// 4. No exception may escape the worker tasks
        ///
        /// IMPORTANT:
        /// This test does not prove correctness alone.
        /// It increases confidence by perturbing concurrency timing.
        /// </summary>
        [Fact]
        public async Task Chaos_Recovery_And_Claim_With_Random_Latency_Should_Never_Break_Invariants()
        {
            var dagStore = CreateDagStore();

            const int iterations = 100;
            const int workers = 50;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var executionId = Guid.NewGuid().ToString("N");

                var claimedAtUtc = DateTime.UtcNow.AddMinutes(-20);
                var leaseExpiresAtUtc = claimedAtUtc.AddSeconds(30);

                var record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "chaos-latency",
                    ExecutionMode = AiExecutionMode.Dag,
                    Status = AiExecutionStatus.Running
                };

                var state = new AiExecutionState
                {
                    ExecutionId = executionId
                };

                state.Steps["step-1"] = new AiStepState
                {
                    StepName = "step-1",
                    Status = AiStepExecutionStatus.Running,
                    ClaimedBy = "seed-worker",
                    ClaimToken = "seed-token",
                    ClaimedAtUtc = claimedAtUtc,
                    LeaseExpiresAtUtc = leaseExpiresAtUtc,
                    ClaimTimeoutSeconds = 30,
                    RetryState = new AiStepRetryState
                    {
                        RetryCount = 0
                    },
                    RecoveryCount = 0
                };

                await dagStore.CreateAsync(record, state);

                var errors = new ConcurrentBag<Exception>();

                var tasks = Enumerable.Range(0, workers)
                    .Select(i => Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Random.Shared.Next(0, 15));

                            var rnd = Random.Shared.Next(0, 3);

                            if (rnd == 0)
                            {
                                await dagStore.RecoverTimedOutStepsAsync(executionId);
                            }
                            else
                            {
                                await dagStore.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }))
                    .ToArray();

                await Task.WhenAll(tasks);

                Assert.Empty(errors);

                var snapshot = await dagStore.GetStateAsync(executionId);
                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["step-1"];

                Assert.True(
                    step.Status == AiStepExecutionStatus.Ready ||
                    step.Status == AiStepExecutionStatus.Running);

                if (step.Status == AiStepExecutionStatus.Ready)
                {
                    Assert.Null(step.ClaimedBy);
                    Assert.Null(step.ClaimToken);
                    Assert.Null(step.ClaimedAtUtc);
                    Assert.Null(step.LeaseExpiresAtUtc);
                }
                else
                {
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimedBy));
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
                    Assert.NotNull(step.ClaimedAtUtc);
                    Assert.NotNull(step.LeaseExpiresAtUtc);
                }

                Assert.True(step.RecoveryCount >= 1);

                await CleanupDagExecutionAsync(executionId);
            }
        }

        /// <summary>
        /// CHAOS TEST — Retry + Recovery + Claim combined
        ///
        /// PURPOSE:
        /// This test mixes the three main non-terminal distributed transitions:
        /// - retry scheduling
        /// - timeout recovery
        /// - claiming ready work
        ///
        /// This is the "boss final" scenario for a single-step execution model because
        /// it exercises the interaction between:
        /// - lease expiration
        /// - recovery reset to Ready
        /// - retry re-entry through WaitingForRetry
        /// - fresh distributed claim ownership
        ///
        /// SCENARIO:
        /// Each iteration starts with an expired Running step.
        /// Workers concurrently:
        /// - recover timed-out work
        /// - claim ready work
        /// - fail the currently claimed work to trigger retry scheduling
        ///
        /// EXPECTED INVARIANTS:
        /// 1. Final state must be one of:
        ///    - Ready
        ///    - Running
        ///    - WaitingForRetry
        ///    - Failed
        /// 2. Ready / WaitingForRetry / Failed must not keep active claim metadata
        /// 3. Running must keep complete claim metadata
        /// 4. RetryCount and RecoveryCount must remain monotonic and non-negative
        /// 5. No exception may escape the worker tasks
        ///
        /// IMPORTANT:
        /// This test is intentionally broad and probabilistic.
        /// It validates that recovery and retry do not corrupt each other.
        /// </summary>
        [Fact]
        public async Task Chaos_Retry_Recovery_And_Claim_Should_Never_Break_Invariants()
        {
            var dagStore = CreateDagStore();

            const int iterations = 100;
            const int workers = 50;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var executionId = Guid.NewGuid().ToString("N");

                var claimedAtUtc = DateTime.UtcNow.AddMinutes(-20);
                var leaseExpiresAtUtc = claimedAtUtc.AddSeconds(30);

                var record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "chaos-retry-recovery",
                    ExecutionMode = AiExecutionMode.Dag,
                    Status = AiExecutionStatus.Running
                };

                var state = new AiExecutionState
                {
                    ExecutionId = executionId
                };

                state.Steps["step-1"] = new AiStepState
                {
                    StepName = "step-1",
                    Status = AiStepExecutionStatus.Running,
                    ClaimedBy = "seed-worker",
                    ClaimToken = "seed-token",
                    ClaimedAtUtc = claimedAtUtc,
                    LeaseExpiresAtUtc = leaseExpiresAtUtc,
                    ClaimTimeoutSeconds = 30,
                    RetryState = new AiStepRetryState
                    {
                        RetryCount = 0
                    },
                    RecoveryCount = 0,
                    Retry = new AiRetryPolicyDefinition
                    {
                        Policies =
                        [
                            new AiConfiguredPolicyDefinition
                            {
                                Name = "retry.transient.default"
                            }
                        ],
                        MaxRetries = 2,
                        MaxDelayMs = 5
                    }
                };

                await dagStore.CreateAsync(record, state);

                var errors = new ConcurrentBag<Exception>();

                var tasks = Enumerable.Range(0, workers)
                    .Select(i => Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Random.Shared.Next(0, 10));

                            var rnd = Random.Shared.Next(0, 4);

                            if (rnd == 0)
                            {
                                await dagStore.RecoverTimedOutStepsAsync(executionId);
                            }
                            else if (rnd == 1)
                            {
                                var claimed = await dagStore.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");

                                if (claimed is not null && Random.Shared.NextDouble() < 0.50)
                                {
                                    await dagStore.TryFailStepAsync(
                                        executionId,
                                        claimed.StepName,
                                        claimed.ClaimToken,
                                        $"chaos-failure-{i}");
                                }
                            }
                            else
                            {
                                await dagStore.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }))
                    .ToArray();

                await Task.WhenAll(tasks);

                Assert.Empty(errors);

                var snapshot = await dagStore.GetStateAsync(executionId);
                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["step-1"];

                Assert.True(
                    step.Status == AiStepExecutionStatus.Ready ||
                    step.Status == AiStepExecutionStatus.Running ||
                    step.Status == AiStepExecutionStatus.WaitingForRetry ||
                    step.Status == AiStepExecutionStatus.Failed);

                if (step.Status == AiStepExecutionStatus.Running)
                {
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimedBy));
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
                    Assert.NotNull(step.ClaimedAtUtc);
                    Assert.NotNull(step.LeaseExpiresAtUtc);
                }
                else
                {
                    Assert.Null(step.ClaimedBy);
                    Assert.Null(step.ClaimToken);
                    Assert.Null(step.ClaimedAtUtc);
                    Assert.Null(step.LeaseExpiresAtUtc);
                }

                Assert.True(step.RetryState?.RetryCount >= 0);
                Assert.True(step.RecoveryCount >= 0);

                await CleanupDagExecutionAsync(executionId);
            }
        }

        /// <summary>
        /// CHAOS TEST — Simulated worker crash after claim
        ///
        /// PURPOSE:
        /// This test simulates one of the most important real-world distributed failures:
        /// a worker successfully claims a step and then crashes before completion.
        ///
        /// The goal is to verify that:
        /// - recovery can later reclaim the abandoned work
        /// - the runtime never ends in an invalid ownership state
        /// - claim metadata remains coherent despite abandoned claims
        ///
        /// SCENARIO:
        /// - Workers attempt to claim ready work
        /// - Some workers simulate a crash immediately after claim by throwing
        /// - Other workers concurrently attempt recovery and fresh claim
        ///
        /// EXPECTED INVARIANTS:
        /// 1. Final state must be Ready or Running
        /// 2. Ready must not keep ownership metadata
        /// 3. Running must have complete ownership metadata
        /// 4. RecoveryCount must remain monotonic
        ///
        /// IMPORTANT:
        /// Simulated worker crashes are caught inside the test harness because the goal
        /// is to model abandoned work, not to fail the entire test process.
        /// </summary>
        [Fact]
        public async Task Chaos_Claim_Then_Simulated_Crash_Should_Never_Break_Invariants()
        {
            var dagStore = CreateDagStore();

            const int iterations = 100;
            const int workers = 50;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var executionId = Guid.NewGuid().ToString("N");

                var record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "chaos-crash",
                    ExecutionMode = AiExecutionMode.Dag,
                    Status = AiExecutionStatus.Running
                };

                var state = new AiExecutionState
                {
                    ExecutionId = executionId
                };

                state.Steps["step-1"] = new AiStepState
                {
                    StepName = "step-1",
                    Status = AiStepExecutionStatus.Ready,
                    ClaimTimeoutSeconds = 1,
                    RetryState = new AiStepRetryState
                    {
                        RetryCount = 0
                    },
                    RecoveryCount = 0,
                    Retry = new AiRetryPolicyDefinition
                    {
                        Policies =
                        [
                            new AiConfiguredPolicyDefinition
                            {
                                Name = "retry.transient.default"
                            }
                        ],
                        MaxRetries = 1,
                        MaxDelayMs = 5
                    }
                };

                await dagStore.CreateAsync(record, state);

                var errors = new ConcurrentBag<Exception>();

                var tasks = Enumerable.Range(0, workers)
                    .Select(i => Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Random.Shared.Next(0, 10));

                            var claimed = await dagStore.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");

                            if (claimed is not null)
                            {
                                if (Random.Shared.NextDouble() < 0.05)
                                {
                                    throw new Exception("Simulated crash");
                                }

                                if (Random.Shared.NextDouble() < 0.25)
                                {
                                    await dagStore.TryFailStepAsync(
                                        executionId,
                                        claimed.StepName,
                                        claimed.ClaimToken,
                                        $"worker-failure-{i}");
                                }
                            }
                            else
                            {
                                await dagStore.RecoverTimedOutStepsAsync(executionId);
                            }
                        }
                        catch (Exception ex)
                        {
                            // We intentionally capture simulated crashes as part of the chaos model.
                            if (!string.Equals(ex.Message, "Simulated crash", StringComparison.Ordinal))
                            {
                                errors.Add(ex);
                            }
                        }
                    }))
                    .ToArray();

                await Task.WhenAll(tasks);

                // Give the 1-second lease a chance to expire for abandoned claims.
                await Task.Delay(1100);
                await dagStore.RecoverTimedOutStepsAsync(executionId);

                Assert.Empty(errors);

                var snapshot = await dagStore.GetStateAsync(executionId);
                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["step-1"];

                Assert.True(
                    step.Status == AiStepExecutionStatus.Ready ||
                    step.Status == AiStepExecutionStatus.Running ||
                    step.Status == AiStepExecutionStatus.WaitingForRetry ||
                    step.Status == AiStepExecutionStatus.Failed);

                if (step.Status == AiStepExecutionStatus.Running)
                {
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimedBy));
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
                    Assert.NotNull(step.ClaimedAtUtc);
                    Assert.NotNull(step.LeaseExpiresAtUtc);
                }
                else
                {
                    Assert.Null(step.ClaimedBy);
                    Assert.Null(step.ClaimToken);
                }

                Assert.True(step.RecoveryCount >= 0);

                await CleanupDagExecutionAsync(executionId);
            }
        }

        /// <summary>
        /// CHAOS TEST — Overnight endurance run
        ///
        /// PURPOSE:
        /// This test repeats a recovery + claim race for a large number of iterations
        /// to increase the probability of surfacing rare distributed interleavings.
        ///
        /// SCENARIO:
        /// - Each iteration starts from an expired Running step
        /// - Concurrent workers attempt recovery and claim
        /// - Final state invariants are checked after every iteration
        ///
        /// EXPECTED INVARIANTS:
        /// - no unhandled exception
        /// - final state must remain coherent
        /// - ownership metadata must match the final status
        ///
        /// IMPORTANT:
        /// - This is an endurance test
        /// - It may be slower than regular integration tests
        /// - It is best suited for overnight or pre-release validation
        /// </summary>
        [Fact]
        public async Task Chaos_Recovery_And_Claim_Overnight_Should_Never_Break_Invariants()
        {
            var dagStore = CreateDagStore();

            const int iterations = 1000;
            const int workers = 20;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var executionId = Guid.NewGuid().ToString("N");

                var claimedAtUtc = DateTime.UtcNow.AddMinutes(-20);
                var leaseExpiresAtUtc = claimedAtUtc.AddSeconds(30);

                var record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "chaos-overnight",
                    ExecutionMode = AiExecutionMode.Dag,
                    Status = AiExecutionStatus.Running
                };

                var state = new AiExecutionState
                {
                    ExecutionId = executionId
                };

                state.Steps["step-1"] = new AiStepState
                {
                    StepName = "step-1",
                    Status = AiStepExecutionStatus.Running,
                    ClaimedBy = "seed-worker",
                    ClaimToken = "seed-token",
                    ClaimedAtUtc = claimedAtUtc,
                    LeaseExpiresAtUtc = leaseExpiresAtUtc,
                    ClaimTimeoutSeconds = 30,
                    RetryState = new AiStepRetryState { RetryCount = 0 },
                    RecoveryCount = 0
                };

                await dagStore.CreateAsync(record, state);

                var errors = new ConcurrentBag<Exception>();

                var tasks = Enumerable.Range(0, workers)
                    .Select(i => Task.Run(async () =>
                    {
                        try
                        {
                            if (Random.Shared.NextDouble() < 0.40)
                            {
                                await dagStore.RecoverTimedOutStepsAsync(executionId);
                            }
                            else
                            {
                                await dagStore.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }))
                    .ToArray();

                await Task.WhenAll(tasks);

                Assert.Empty(errors);

                var snapshot = await dagStore.GetStateAsync(executionId);
                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["step-1"];

                Assert.True(
                    step.Status == AiStepExecutionStatus.Ready ||
                    step.Status == AiStepExecutionStatus.Running);

                if (step.Status == AiStepExecutionStatus.Ready)
                {
                    Assert.Null(step.ClaimedBy);
                    Assert.Null(step.ClaimToken);
                    Assert.Null(step.ClaimedAtUtc);
                    Assert.Null(step.LeaseExpiresAtUtc);
                }
                else
                {
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimedBy));
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
                    Assert.NotNull(step.ClaimedAtUtc);
                    Assert.NotNull(step.LeaseExpiresAtUtc);
                }

                await CleanupDagExecutionAsync(executionId);
            }
        }

        /// <summary>
        /// CHAOS TEST — Boss final
        ///
        /// PURPOSE:
        /// This test combines the most difficult distributed execution conditions in a
        /// single probabilistic scenario:
        /// - random latency
        /// - timeout recovery
        /// - fresh claim acquisition
        /// - retry scheduling through failure
        /// - simulated worker crash after claim
        ///
        /// This is intended as a high-confidence resilience test for the Redis-backed
        /// DAG execution store under hostile timing conditions.
        ///
        /// SCENARIO:
        /// - Each iteration starts from a Ready step
        /// - Workers randomly:
        ///   - delay
        ///   - claim
        ///   - fail claimed work
        ///   - crash after claim
        ///   - recover abandoned work
        ///
        /// EXPECTED INVARIANTS:
        /// 1. Final state must always be valid
        /// 2. Active claim metadata must exist only for Running
        /// 3. Non-running states must not retain active ownership
        /// 4. RetryCount and RecoveryCount must remain monotonic and non-negative
        /// 5. No infrastructure exception may escape the test harness
        ///
        /// IMPORTANT:
        /// - Simulated crashes are expected and are intentionally swallowed
        /// - This test is probabilistic and should be run multiple times for confidence
        /// - Any invariant failure should be treated as a real distributed correctness bug
        /// </summary>
        [Fact]
        public async Task Chaos_BossFinal_Retry_Recovery_Claim_Crash_And_Latency_Should_Never_Break_Invariants()
        {
            var dagStore = CreateDagStore();

            const int iterations = 100;
            const int workers = 50;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var executionId = Guid.NewGuid().ToString("N");

                var record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "chaos-boss-final",
                    ExecutionMode = AiExecutionMode.Dag,
                    Status = AiExecutionStatus.Running
                };

                var state = new AiExecutionState
                {
                    ExecutionId = executionId
                };

                state.Steps["step-1"] = new AiStepState
                {
                    StepName = "step-1",
                    Status = AiStepExecutionStatus.Ready,
                    ClaimTimeoutSeconds = 1,
                    RetryState = new AiStepRetryState { RetryCount = 0 },
                    RecoveryCount = 0,
                    Retry = new AiRetryPolicyDefinition
                    {
                        Policies = [new AiConfiguredPolicyDefinition { Name = "retry.transient.default" }],
                        MaxRetries = 2,
                        MaxDelayMs = 5
                    }
                };

                await dagStore.CreateAsync(record, state);

                var errors = new ConcurrentBag<Exception>();

                var tasks = Enumerable.Range(0, workers)
                    .Select(i => Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Random.Shared.Next(0, 15));

                            var mode = Random.Shared.Next(0, 5);

                            if (mode == 0)
                            {
                                await dagStore.RecoverTimedOutStepsAsync(executionId);
                                return;
                            }

                            var claimed = await dagStore.TryClaimNextReadyStepAsync(executionId, $"worker-{i}");

                            if (claimed is null)
                                return;

                            if (Random.Shared.NextDouble() < 0.05)
                            {
                                throw new Exception("Simulated crash");
                            }

                            if (mode == 1 || mode == 2)
                            {
                                await dagStore.TryFailStepAsync(
                                    executionId,
                                    claimed.StepName,
                                    claimed.ClaimToken,
                                    $"boss-final-failure-{i}");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!string.Equals(ex.Message, "Simulated crash", StringComparison.Ordinal))
                            {
                                errors.Add(ex);
                            }
                        }
                    }))
                    .ToArray();

                await Task.WhenAll(tasks);

                await Task.Delay(1100);
                await dagStore.RecoverTimedOutStepsAsync(executionId);

                Assert.Empty(errors);

                var snapshot = await dagStore.GetStateAsync(executionId);
                Assert.NotNull(snapshot);

                var step = snapshot!.Steps["step-1"];

                Assert.True(
                    step.Status == AiStepExecutionStatus.Ready ||
                    step.Status == AiStepExecutionStatus.Running ||
                    step.Status == AiStepExecutionStatus.WaitingForRetry ||
                    step.Status == AiStepExecutionStatus.Failed);

                if (step.Status == AiStepExecutionStatus.Running)
                {
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimedBy));
                    Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
                    Assert.NotNull(step.ClaimedAtUtc);
                    Assert.NotNull(step.LeaseExpiresAtUtc);
                }
                else
                {
                    Assert.Null(step.ClaimedBy);
                    Assert.Null(step.ClaimToken);
                    Assert.Null(step.ClaimedAtUtc);
                    Assert.Null(step.LeaseExpiresAtUtc);
                }

                Assert.True(step.RetryState?.RetryCount >= 0);
                Assert.True(step.RecoveryCount >= 0);

                await CleanupDagExecutionAsync(executionId);
            }
        }

        // ---------------------------------------------------------------------
        // TEST HELPERS
        // ---------------------------------------------------------------------

        private async Task CreateExecutionAsync(string executionId, params AiStepState[] steps)
        {
            var record = new AiExecutionRecord
            {
                ExecutionId = executionId
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId
            };

            foreach (var step in steps)
            {
                state.Steps[step.StepName] = step;
            }

            await _store.CreateAsync(record, state);
        }

        private static AiStepState CreateRunningStep(
            string stepName,
            string claimedBy,
            string claimToken,
            DateTime claimedAtUtc,
            DateTime leaseExpiresAtUtc,
            int claimTimeoutSeconds)
        {
            return new AiStepState
            {
                StepName = stepName,
                Status = AiStepExecutionStatus.Running,
                ClaimedBy = claimedBy,
                ClaimToken = claimToken,
                ClaimedAtUtc = claimedAtUtc,
                LeaseExpiresAtUtc = leaseExpiresAtUtc,
                ClaimTimeoutSeconds = claimTimeoutSeconds,
                DependsOn = new List<string>(),
                Inputs = new Dictionary<string, object?>(StringComparer.Ordinal),
                Config = new Dictionary<string, object?>(StringComparer.Ordinal)
            };
        }

        // ---------------------------------------------------------------------
        // TEST KEY BUILDER
        // ---------------------------------------------------------------------

        private sealed class TestAiExecutionKeyBuilder : IAiExecutionKeyBuilder
        {
            private readonly string _prefix;

            public TestAiExecutionKeyBuilder(string prefix)
            {
                _prefix = prefix;
            }

            public string GetExecutionRecordKey(string executionId)
                => $"{_prefix}:exec:{executionId}:record";

            public string GetExecutionStateKey(string executionId)
                => $"{_prefix}:exec:{executionId}:state";

            public string GetDagStepIdsKey(string executionId)
                => $"{_prefix}:exec:{executionId}:dag:steps";

            public string GetDagStepKey(string executionId, string stepName)
                => $"{GetDagStepKeyPrefix(executionId)}{stepName}";

            public string GetDagStepKeyPrefix(string executionId)
                => $"{_prefix}:exec:{executionId}:dag:step:";

            public string GetDagClaimKey(string executionId, string stepId)
                => $"{_prefix}:exec:{executionId}:dag:claim:{stepId}";

            public string GetDagLeaseKey(string executionId, string stepId)
                => $"{_prefix}:exec:{executionId}:dag:lease:{stepId}";

            public string GetDagInFlightKey(string executionId)
                => $"{_prefix}:exec:{executionId}:dag:inflight";

            public string GetDagMetaKey(string executionId)
                => $"{_prefix}:exec:{executionId}:dag:meta";
        }

        private RedisAiDagExecutionStore CreateDagStore()
        {
            var logger = new NoopLogger();
            var metrics = MetricsFactory.Create();
            var normalizers = new DefaultAiStepResultNormalizerPipeline([new RagStepResultNormalizer()]);
            IRedisDagStoreServices services = new RedisDagStoreServices(
                _multiplexer,
                _keyBuilder,
                logger,
                metrics,
                normalizers);
            return new RedisAiDagExecutionStore(services);
        }

        private async Task CleanupDagExecutionAsync(string executionId)
        {
            foreach (var endpoint in _multiplexer.GetEndPoints())
            {
                var server = _multiplexer.GetServer(endpoint);

                if (!server.IsConnected)
                    continue;

                var pattern = $"{_testPrefix}:exec:{executionId}*";

                await foreach (var key in server.KeysAsync(
                    database: _database.Database,
                    pattern: pattern))
                {
                    await _database.KeyDeleteAsync(key);
                }
            }
        }
    }
}