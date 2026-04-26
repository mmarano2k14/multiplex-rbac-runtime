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
    /// Integration tests validating Redis round-trip persistence for
    /// <see cref="RedisAiExecutionStore"/>.
    ///
    /// PURPOSE:
    /// - Validate execution record and state persistence.
    /// - Validate Redis JSON round-trip behavior.
    /// - Validate alignment between orchestration record and execution state.
    ///
    /// ARCHITECTURE:
    /// - <see cref="AiExecutionState"/> is treated as a persistence model.
    /// - State mutation is performed through <see cref="IAiExecutionStateWriter"/>.
    /// - State reading is performed through <see cref="IAiExecutionStateReader"/>.
    /// </summary>
    [Collection("redis")]
    public sealed class RedisAiExecutionStoreRoundTripTests
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly RedisAiExecutionStore _store;
        private readonly IAiExecutionStateWriter _stateWriter;
        private readonly IAiExecutionStateReader _stateReader;

        public RedisAiExecutionStoreRoundTripTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);

            var keyBuilder = new AiExecutionKeyBuilder();

            _connection = fixture.Connection;
            _store = new RedisAiExecutionStore(_connection, keyBuilder);
            _stateWriter = new DefaultAiExecutionStateWriter();
            _stateReader = new DefaultAiExecutionStateReader(new NoopPayloadResolver());
        }

        [RedisFact]
        public async Task CreateAsync_Should_Persist_Record_And_State()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "roundtrip-pipeline",
                ContextKey = "ctx-roundtrip",
                CurrentStepIndex = 0,
                CurrentStep = "hello",
                ExecutionStepKey = "step-key-1",
                Status = AiExecutionStatus.Running,
                Version = 1,
                Steps = new() { "hello", "summary" },
                CompletedSteps = new(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "roundtrip-pipeline"
            };

            _stateWriter.SetData(state, "input", "hello world");
            _stateWriter.SetData(state, "count", 42);
            _stateWriter.SetData(state, "flag", true);
            _stateWriter.SetData(state, "tags", new List<string> { "a", "b", "c" });

            _stateWriter.SetMetadata(state, "traceId", "trace-123");
            _stateWriter.SetMetadata(state, "attempt", 1);

            await _store.CreateAsync(record, state);

            var storedRecord = await _store.GetRecordAsync(executionId);
            var storedState = await _store.GetStateAsync(executionId);

            Assert.NotNull(storedRecord);
            Assert.NotNull(storedState);

            Assert.Equal(record.ExecutionId, storedRecord!.ExecutionId);
            Assert.Equal(record.PipelineName, storedRecord.PipelineName);
            Assert.Equal(record.ContextKey, storedRecord.ContextKey);
            Assert.Equal(record.CurrentStepIndex, storedRecord.CurrentStepIndex);
            Assert.Equal(record.CurrentStep, storedRecord.CurrentStep);
            Assert.Equal(record.ExecutionStepKey, storedRecord.ExecutionStepKey);
            Assert.Equal(record.Status, storedRecord.Status);
            Assert.Equal(record.Version, storedRecord.Version);
            Assert.Equal(record.Steps, storedRecord.Steps);
            Assert.Equal(record.CompletedSteps, storedRecord.CompletedSteps);

            Assert.Equal("hello world", await _stateReader.GetDataAsync<string>(storedState!, "input"));
            Assert.Equal(42, await _stateReader.GetDataAsync<int>(storedState!, "count"));
            Assert.True(await _stateReader.GetDataAsync<bool>(storedState!, "flag"));

            var tags = await _stateReader.GetDataAsync<List<string>>(storedState!, "tags");

            Assert.NotNull(tags);
            Assert.Equal(new[] { "a", "b", "c" }, tags);

            Assert.Equal("trace-123", await _stateReader.GetMetadataAsync<string>(storedState!, "traceId"));
            Assert.Equal(1, await _stateReader.GetMetadataAsync<int>(storedState!, "attempt"));
        }

        [RedisFact]
        public async Task GetRecordAsync_Should_Return_Null_When_Record_Does_Not_Exist()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var record = await _store.GetRecordAsync(executionId);

            Assert.Null(record);
        }

        [RedisFact]
        public async Task GetStateAsync_Should_Return_Null_When_State_Does_Not_Exist()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var state = await _store.GetStateAsync(executionId);

            Assert.Null(state);
        }

        [RedisFact]
        public async Task CreateAsync_Should_Overwrite_Previous_Record_And_State_For_Same_ExecutionId()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var initialRecord = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "pipeline-v1",
                ContextKey = "ctx-v1",
                CurrentStepIndex = 0,
                CurrentStep = "hello",
                ExecutionStepKey = "step-key-1",
                Status = AiExecutionStatus.Pending,
                Version = 1,
                Steps = new() { "hello", "summary" },
                CompletedSteps = new()
            };

            var initialState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "pipeline-v1"
            };

            _stateWriter.SetData(initialState, "input", "first");
            _stateWriter.SetMetadata(initialState, "traceId", "trace-v1");

            await _store.CreateAsync(initialRecord, initialState);

            var updatedRecord = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "pipeline-v2",
                ContextKey = "ctx-v2",
                CurrentStepIndex = 1,
                CurrentStep = "summary",
                ExecutionStepKey = "step-key-2",
                Status = AiExecutionStatus.Running,
                Version = 2,
                Steps = new() { "hello", "summary" },
                CompletedSteps = new() { "hello" }
            };

            var updatedState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "pipeline-v2"
            };

            _stateWriter.SetData(updatedState, "input", "second");
            _stateWriter.SetData(updatedState, "score", 99);
            _stateWriter.SetMetadata(updatedState, "traceId", "trace-v2");

            await _store.CreateAsync(updatedRecord, updatedState);

            var storedRecord = await _store.GetRecordAsync(executionId);
            var storedState = await _store.GetStateAsync(executionId);

            Assert.NotNull(storedRecord);
            Assert.NotNull(storedState);

            Assert.Equal("pipeline-v2", storedRecord!.PipelineName);
            Assert.Equal("ctx-v2", storedRecord.ContextKey);
            Assert.Equal(1, storedRecord.CurrentStepIndex);
            Assert.Equal("summary", storedRecord.CurrentStep);
            Assert.Equal("step-key-2", storedRecord.ExecutionStepKey);
            Assert.Equal(AiExecutionStatus.Running, storedRecord.Status);
            Assert.Equal(2, storedRecord.Version);
            Assert.Single(storedRecord.CompletedSteps);
            Assert.Contains("hello", storedRecord.CompletedSteps);

            Assert.Equal("second", await _stateReader.GetDataAsync<string>(storedState!, "input"));
            Assert.Equal(99, await _stateReader.GetDataAsync<int>(storedState!, "score"));
            Assert.Equal("trace-v2", await _stateReader.GetMetadataAsync<string>(storedState!, "traceId"));
        }

        [RedisFact]
        public async Task CreateAsync_Should_Preserve_Complex_Object_RoundTrip()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "complex-pipeline",
                ContextKey = "ctx-complex",
                CurrentStepIndex = 0,
                CurrentStep = "analyze",
                ExecutionStepKey = "step-key-complex",
                Status = AiExecutionStatus.Running,
                Version = 1,
                Steps = new() { "analyze", "complete" },
                CompletedSteps = new()
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "complex-pipeline"
            };

            _stateWriter.SetData(state, "payload", new TestPayload
            {
                Name = "Marco",
                Count = 3,
                Tags = new List<string> { "redis", "roundtrip" }
            });

            await _store.CreateAsync(record, state);

            var storedState = await _store.GetStateAsync(executionId);

            Assert.NotNull(storedState);

            var payload = await _stateReader.GetDataAsync<TestPayload>(
                storedState!,
                "payload");

            Assert.NotNull(payload);
            Assert.Equal("Marco", payload!.Name);
            Assert.Equal(3, payload.Count);
            Assert.Equal(new[] { "redis", "roundtrip" }, payload.Tags);
        }

        [RedisFact]
        public async Task Record_And_State_Should_Remain_Aligned_After_RoundTrip()
        {
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "alignment-pipeline",
                ContextKey = "ctx-aligned",
                CurrentStepIndex = 2,
                CurrentStep = "finalize",
                ExecutionStepKey = "aligned-step-key",
                Status = AiExecutionStatus.Running,
                Version = 7,
                Steps = new() { "start", "process", "finalize" },
                CompletedSteps = new() { "start", "process" }
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "alignment-pipeline"
            };

            _stateWriter.SetData(state, "result", "ok");
            _stateWriter.SetMetadata(state, "traceId", "trace-aligned");

            await _store.CreateAsync(record, state);

            var storedRecord = await _store.GetRecordAsync(executionId);
            var storedState = await _store.GetStateAsync(executionId);

            Assert.NotNull(storedRecord);
            Assert.NotNull(storedState);

            Assert.Equal(storedRecord!.ExecutionId, storedState!.ExecutionId);
            Assert.Equal(storedRecord.PipelineName, storedState.PipelineName);
            Assert.Equal("finalize", storedRecord.CurrentStep);

            Assert.Equal("ok", await _stateReader.GetDataAsync<string>(storedState!, "result"));
            Assert.Equal("trace-aligned", await _stateReader.GetMetadataAsync<string>(storedState!, "traceId"));
        }

        /// <summary>
        /// Removes the Redis keys associated with a test execution id.
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

        private sealed class TestPayload
        {
            public string Name { get; set; } = string.Empty;

            public int Count { get; set; }

            public List<string> Tags { get; set; } = new();
        }

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
                    "Payload resolution is not expected in this Redis round-trip test.");
            }
        }
    }
}