using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Stores
{
    /// <summary>
    /// Integration tests for <see cref="RedisAiExecutionStore"/>.
    ///
    /// PURPOSE:
    /// - Validate optimistic concurrency behavior.
    /// - Validate isolated Redis snapshots.
    /// - Validate consistency between execution record and execution state.
    ///
    /// ARCHITECTURE:
    /// - <see cref="AiExecutionState"/> is treated as a persistence model.
    /// - State mutation is performed through <see cref="IAiExecutionStateWriter"/>.
    /// - State reading is performed through <see cref="IAiExecutionStateReader"/>.
    /// </summary>
    [Collection("redis")]
    public sealed class RedisAiExecutionStoreConcurrencyTests
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly RedisAiExecutionStore _store;
        private readonly IAiExecutionStateWriter _stateWriter;
        private readonly IAiExecutionStateReader _stateReader;

        public RedisAiExecutionStoreConcurrencyTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);

            var keyBuilder = new AiExecutionKeyBuilder();

            _connection = fixture.Connection;
            _store = new RedisAiExecutionStore(_connection, keyBuilder);
            _stateWriter = new DefaultAiExecutionStateWriter();
            _stateReader = new DefaultAiExecutionStateReader(new NoopPayloadResolver());
        }

        /// <summary>
        /// Validates that only one concurrent update succeeds when two workers
        /// update the same execution snapshot using the same expected step key.
        /// </summary>
        [RedisFact]
        public async Task TryUpdateAsync_Should_Allow_Only_One_Concurrent_Update()
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

            _stateWriter.SetData(initialState, "input", "hello world");

            await _store.CreateAsync(initialRecord, initialState);

            var record1 = await _store.GetRecordAsync(executionId);
            var state1 = await _store.GetStateAsync(executionId);

            var record2 = await _store.GetRecordAsync(executionId);
            var state2 = await _store.GetStateAsync(executionId);

            Assert.NotNull(record1);
            Assert.NotNull(record2);
            Assert.NotNull(state1);
            Assert.NotNull(state2);

            record1!.CompletedSteps.Add("hello");
            record1.CurrentStepIndex = 1;
            record1.CurrentStep = "summary";
            record1.TouchVersion();
            record1.RenewExecutionStepKey();

            _stateWriter.SetData(state1!, "branch", "update-1");

            record2!.CompletedSteps.Add("hello");
            record2.CurrentStepIndex = 1;
            record2.CurrentStep = "summary";
            record2.TouchVersion();
            record2.RenewExecutionStepKey();

            _stateWriter.SetData(state2!, "branch", "update-2");

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

            Assert.Equal(1, results.Count(x => x));
            Assert.Equal(1, results.Count(x => !x));

            var finalRecord = await _store.GetRecordAsync(executionId);
            var finalState = await _store.GetStateAsync(executionId);

            Assert.NotNull(finalRecord);
            Assert.NotNull(finalState);

            Assert.Single(finalRecord!.CompletedSteps);
            Assert.Contains("hello", finalRecord.CompletedSteps);
            Assert.Equal(1, finalRecord.CurrentStepIndex);
            Assert.Equal("summary", finalRecord.CurrentStep);

            var branch = await _stateReader.GetDataAsync<string>(
                finalState!,
                "branch");

            Assert.True(branch is "update-1" or "update-2");
        }

        /// <summary>
        /// Validates that an update is rejected when the expected execution step key
        /// does not match the persisted record.
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

            Assert.NotNull(updatedRecord);
            Assert.NotNull(updatedState);

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
        /// Validates that Redis returns isolated record and state copies.
        ///
        /// PURPOSE:
        /// - Prevent shared-reference mutation between workers.
        /// - Ensure one loaded snapshot cannot mutate another loaded snapshot.
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

            _stateWriter.SetData(initialState, "input", "hello world");

            await _store.CreateAsync(initialRecord, initialState);

            var recordA = await _store.GetRecordAsync(executionId);
            var stateA = await _store.GetStateAsync(executionId);

            var recordB = await _store.GetRecordAsync(executionId);
            var stateB = await _store.GetStateAsync(executionId);

            Assert.NotNull(recordA);
            Assert.NotNull(stateA);
            Assert.NotNull(recordB);
            Assert.NotNull(stateB);

            recordA!.CompletedSteps.Add("mutated");
            _stateWriter.SetData(stateA!, "input", "changed");

            Assert.DoesNotContain("mutated", recordB!.CompletedSteps);

            var input = await _stateReader.GetDataAsync<string>(
                stateB!,
                "input");

            Assert.Equal("hello world", input);
        }

        /// <summary>
        /// Removes Redis keys for a given execution.
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

        /// <summary>
        /// Payload resolver placeholder.
        ///
        /// This test only uses inline execution state values.
        /// Payload resolution is not expected.
        /// </summary>
        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this Redis execution store test.");
            }
        }
    }
}