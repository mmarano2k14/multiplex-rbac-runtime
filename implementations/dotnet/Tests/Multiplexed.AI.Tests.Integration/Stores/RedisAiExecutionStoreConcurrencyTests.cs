using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Stores
{
    /// <summary>
    /// 🔴 Integration tests for RedisAiExecutionStore
    /// 
    /// These tests validate:
    /// - optimistic concurrency behavior (CAS-like semantics)
    /// - data isolation (deep copy guarantees)
    /// - consistency between record and state
    ///
    /// IMPORTANT:
    /// These are NOT unit tests → they validate real Redis behavior.
    /// </summary>
    [Collection("redis")]
    public sealed class RedisAiExecutionStoreConcurrencyTests
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly RedisAiExecutionStore _store;

        public RedisAiExecutionStoreConcurrencyTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            var keyBuilder = new AiExecutionKeyBuilder();
            _connection = fixture.Connection;
            _store = new RedisAiExecutionStore(_connection, keyBuilder);
        }

        /// <summary>
        /// 🧪 CORE TEST: Concurrency control
        ///
        /// Simulates 2 concurrent updates on the SAME execution.
        ///
        /// Expected behavior:
        /// - Only ONE update succeeds
        /// - The other must fail (optimistic concurrency)
        ///
        /// This validates:
        /// - atomicity of Redis operation (Lua / WATCH / transaction)
        /// - correctness of ExecutionStepKey check
        /// </summary>
        [RedisFact]
        public async Task TryUpdateAsync_Should_Allow_Only_One_Concurrent_Update()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            // Initial execution record (step 0)
            var initialRecord = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                ContextKey = "ctx-test",
                CurrentStepIndex = 0,
                CurrentStep = "hello",
                ExecutionStepKey = "step-key-1",
                Status = AiExecutionStatus.Running,
                Version = 1,
                Steps = new() { "hello", "summary" },
                CompletedSteps = new()
            };

            // Initial state
            var initialState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            initialState.Set("input", "hello world");

            await _store.CreateAsync(initialRecord, initialState);

            // 🔁 Load TWO independent copies (simulate 2 workers)
            var record1 = await _store.GetRecordAsync(executionId);
            var state1 = await _store.GetStateAsync(executionId);

            var record2 = await _store.GetRecordAsync(executionId);
            var state2 = await _store.GetStateAsync(executionId);

            Assert.NotNull(record1);
            Assert.NotNull(record2);
            Assert.NotNull(state1);
            Assert.NotNull(state2);

            // Worker 1 mutation
            record1!.CompletedSteps.Add("hello");
            record1.CurrentStepIndex = 1;
            record1.CurrentStep = "summary";
            record1.TouchVersion();
            record1.RenewExecutionStepKey();
            state1!.Set("branch", "update-1");

            // Worker 2 mutation (same base version → conflict expected)
            record2!.CompletedSteps.Add("hello");
            record2.CurrentStepIndex = 1;
            record2.CurrentStep = "summary";
            record2.TouchVersion();
            record2.RenewExecutionStepKey();
            state2!.Set("branch", "update-2");

            // Barrier ensures TRUE concurrency
            using var barrier = new Barrier(2);

            var t1 = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await _store.TryUpdateAsync(
                    executionId,
                    "step-key-1",
                    record1,
                    state1);
            });

            var t2 = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await _store.TryUpdateAsync(
                    executionId,
                    "step-key-1",
                    record2,
                    state2);
            });

            await Task.WhenAll(t1, t2);

            var results = new[] { t1.Result, t2.Result };

            // ✅ EXACTLY ONE must succeed
            Assert.Equal(1, results.Count(x => x));
            Assert.Equal(1, results.Count(x => !x));

            // Reload final state
            var finalRecord = await _store.GetRecordAsync(executionId);
            var finalState = await _store.GetStateAsync(executionId);

            Assert.NotNull(finalRecord);
            Assert.NotNull(finalState);

            // Ensure consistency
            Assert.Single(finalRecord!.CompletedSteps);
            Assert.Contains("hello", finalRecord.CompletedSteps);
            Assert.Equal(1, finalRecord.CurrentStepIndex);
            Assert.Equal("summary", finalRecord.CurrentStep);

            // Only one branch should exist
            var branch = finalState!.Get<string>("branch");
            Assert.True(branch is "update-1" or "update-2");
        }

        /// <summary>
        /// 🧪 Negative test: wrong step key
        ///
        /// Expected:
        /// - update MUST fail
        ///
        /// Validates:
        /// - deterministic execution guard
        /// - protection against out-of-order execution
        /// </summary>
        [RedisFact]
        public async Task TryUpdateAsync_Should_Return_False_When_ExpectedStepKey_Does_Not_Match()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var initialRecord = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                ContextKey = "ctx-test",
                CurrentStepIndex = 0,
                CurrentStep = "hello",
                ExecutionStepKey = "step-key-1",
                Status = AiExecutionStatus.Running,
                Version = 1,
                Steps = new() { "hello", "summary" },
                CompletedSteps = new()
            };

            var initialState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            await _store.CreateAsync(initialRecord, initialState);

            var updatedRecord = await _store.GetRecordAsync(executionId);
            var updatedState = await _store.GetStateAsync(executionId);

            updatedRecord!.CompletedSteps.Add("hello");
            updatedRecord.CurrentStepIndex = 1;
            updatedRecord.CurrentStep = "summary";
            updatedRecord.TouchVersion();
            updatedRecord.RenewExecutionStepKey();

            var result = await _store.TryUpdateAsync(
                executionId,
                "wrong-step-key",
                updatedRecord,
                updatedState!);

            Assert.False(result);
        }

        /// <summary>
        /// 🧪 Isolation test (CRITICAL)
        ///
        /// Validates that:
        /// - Redis store returns DEEP COPIES
        /// - no shared references between calls
        ///
        /// This prevents:
        /// - cross-thread mutation bugs
        /// - memory corruption
        /// - hidden race conditions
        /// </summary>
        [RedisFact]
        public async Task GetRecordAsync_And_GetStateAsync_Should_Return_Isolated_Copies()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var initialRecord = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                ContextKey = "ctx-test",
                CurrentStepIndex = 0,
                CurrentStep = "hello",
                ExecutionStepKey = "step-key-1",
                Status = AiExecutionStatus.Running,
                Version = 1,
                Steps = new() { "hello", "summary" },
                CompletedSteps = new()
            };

            var initialState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            initialState.Set("input", "hello world");

            await _store.CreateAsync(initialRecord, initialState);

            var recordA = await _store.GetRecordAsync(executionId);
            var stateA = await _store.GetStateAsync(executionId);

            var recordB = await _store.GetRecordAsync(executionId);
            var stateB = await _store.GetStateAsync(executionId);

            // Mutate A
            recordA!.CompletedSteps.Add("mutated");
            stateA!.Set("input", "changed");

            // Ensure B is NOT impacted
            Assert.DoesNotContain("mutated", recordB!.CompletedSteps);

            // ⚠️ Important: requires JsonElement fix in AiExecutionState
            Assert.Equal("hello world", stateB!.Get<string>("input"));
        }

        /// <summary>
        /// 🧹 Cleanup helper
        ///
        /// Ensures:
        /// - test isolation
        /// - no cross-test pollution
        /// </summary>
        private async Task CleanupExecutionAsync(string executionId)
        {
            var db = _connection.GetDatabase();

            await db.KeyDeleteAsync(GetRecordKey(executionId));
            await db.KeyDeleteAsync(GetStateKey(executionId));
        }

        private static string GetRecordKey(string executionId)
            => $"ai:execution:record:{executionId}";

        private static string GetStateKey(string executionId)
            => $"ai:execution:state:{executionId}";
    }
}