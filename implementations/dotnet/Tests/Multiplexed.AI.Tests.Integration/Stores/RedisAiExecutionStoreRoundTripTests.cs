using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Stores
{
    /// <summary>
    /// Integration tests validating Redis round-trip persistence for
    /// <see cref="RedisAiExecutionStore"/>.
    ///
    /// These tests focus on:
    /// - Persisting execution records and states
    /// - Reading them back from Redis
    /// - Ensuring data survives JSON serialization/deserialization
    /// - Verifying alignment between orchestration record and mutable state
    /// </summary>
    [Collection("redis")]
    public sealed class RedisAiExecutionStoreRoundTripTests
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly RedisAiExecutionStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisAiExecutionStoreRoundTripTests"/> class.
        /// </summary>
        public RedisAiExecutionStoreRoundTripTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            var keyBuilder = new AiExecutionKeyBuilder();

            _connection = fixture.Connection;
            _store = new RedisAiExecutionStore(_connection, keyBuilder);
        }

        /// <summary>
        /// Verifies that <see cref="RedisAiExecutionStore.CreateAsync"/> persists
        /// both the orchestration record and the mutable execution state.
        /// </summary>
        [RedisFact]
        public async Task CreateAsync_Should_Persist_Record_And_State()
        {
            // Arrange
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

            state.Set("input", "hello world");
            state.Set("count", 42);
            state.Set("flag", true);
            state.Set("tags", new List<string> { "a", "b", "c" });

            state.SetMetadata("traceId", "trace-123");
            state.SetMetadata("attempt", 1);

            // Act
            await _store.CreateAsync(record, state);

            var storedRecord = await _store.GetRecordAsync(executionId);
            var storedState = await _store.GetStateAsync(executionId);

            // Assert
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

            Assert.Equal("hello world", storedState!.Get<string>("input"));
            Assert.Equal(42, storedState.Get<int>("count"));
            Assert.True(storedState.Get<bool>("flag"));

            var tags = storedState.Get<List<string>>("tags");
            Assert.NotNull(tags);
            Assert.Equal(new[] { "a", "b", "c" }, tags);

            Assert.Equal("trace-123", storedState.GetMetadata<string>("traceId"));
            Assert.Equal(1, storedState.GetMetadata<int>("attempt"));
        }

        /// <summary>
        /// Verifies that requesting a non-existent record returns <c>null</c>.
        /// </summary>
        [RedisFact]
        public async Task GetRecordAsync_Should_Return_Null_When_Record_Does_Not_Exist()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            // Act
            var record = await _store.GetRecordAsync(executionId);

            // Assert
            Assert.Null(record);
        }

        /// <summary>
        /// Verifies that requesting a non-existent state returns <c>null</c>.
        /// </summary>
        [RedisFact]
        public async Task GetStateAsync_Should_Return_Null_When_State_Does_Not_Exist()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString("N");
            await CleanupExecutionAsync(executionId);

            // Act
            var state = await _store.GetStateAsync(executionId);

            // Assert
            Assert.Null(state);
        }

        /// <summary>
        /// Verifies that creating a record/state pair with the same execution id
        /// overwrites the previously stored values.
        /// </summary>
        [RedisFact]
        public async Task CreateAsync_Should_Overwrite_Previous_Record_And_State_For_Same_ExecutionId()
        {
            // Arrange
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

            initialState.Set("input", "first");
            initialState.SetMetadata("traceId", "trace-v1");

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

            updatedState.Set("input", "second");
            updatedState.Set("score", 99);
            updatedState.SetMetadata("traceId", "trace-v2");

            // Act
            await _store.CreateAsync(updatedRecord, updatedState);

            var storedRecord = await _store.GetRecordAsync(executionId);
            var storedState = await _store.GetStateAsync(executionId);

            // Assert
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

            Assert.Equal("second", storedState!.Get<string>("input"));
            Assert.Equal(99, storedState.Get<int>("score"));
            Assert.Equal("trace-v2", storedState.GetMetadata<string>("traceId"));
        }

        /// <summary>
        /// Verifies that complex object payloads survive a full Redis JSON round-trip.
        /// </summary>
        [RedisFact]
        public async Task CreateAsync_Should_Preserve_Complex_Object_RoundTrip()
        {
            // Arrange
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

            state.Set("payload", new TestPayload
            {
                Name = "Marco",
                Count = 3,
                Tags = new List<string> { "redis", "roundtrip" }
            });

            // Act
            await _store.CreateAsync(record, state);

            var storedState = await _store.GetStateAsync(executionId);

            // Assert
            Assert.NotNull(storedState);

            var payload = storedState!.Get<TestPayload>("payload");
            Assert.NotNull(payload);
            Assert.Equal("Marco", payload!.Name);
            Assert.Equal(3, payload.Count);
            Assert.Equal(new[] { "redis", "roundtrip" }, payload.Tags);
        }

        /// <summary>
        /// Verifies that the stored orchestration record and execution state
        /// remain aligned after a Redis round-trip.
        /// </summary>
        [RedisFact]
        public async Task Record_And_State_Should_Remain_Aligned_After_RoundTrip()
        {
            // Arrange
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

            state.Set("result", "ok");
            state.SetMetadata("traceId", "trace-aligned");

            // Act
            await _store.CreateAsync(record, state);

            var storedRecord = await _store.GetRecordAsync(executionId);
            var storedState = await _store.GetStateAsync(executionId);

            // Assert
            Assert.NotNull(storedRecord);
            Assert.NotNull(storedState);

            Assert.Equal(storedRecord!.ExecutionId, storedState!.ExecutionId);
            Assert.Equal(storedRecord.PipelineName, storedState.PipelineName);
            Assert.Equal("finalize", storedRecord.CurrentStep);
            Assert.Equal("ok", storedState.Get<string>("result"));
            Assert.Equal("trace-aligned", storedState.GetMetadata<string>("traceId"));
        }

        /// <summary>
        /// Removes the Redis keys associated with a test execution id.
        /// This keeps tests isolated and repeatable.
        /// </summary>
        private async Task CleanupExecutionAsync(string executionId)
        {
            var db = _connection.GetDatabase();

            await db.KeyDeleteAsync(GetRecordKey(executionId));
            await db.KeyDeleteAsync(GetStateKey(executionId));
        }

        /// <summary>
        /// Builds the Redis key used for execution records.
        /// </summary>
        private static string GetRecordKey(string executionId)
            => $"ai:execution:record:{executionId}";

        /// <summary>
        /// Builds the Redis key used for execution states.
        /// </summary>
        private static string GetStateKey(string executionId)
            => $"ai:execution:state:{executionId}";

        /// <summary>
        /// Simple payload used to validate complex object round-trip behavior.
        /// </summary>
        private sealed class TestPayload
        {
            /// <summary>
            /// Gets or sets the payload name.
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the payload count.
            /// </summary>
            public int Count { get; set; }

            /// <summary>
            /// Gets or sets the payload tags.
            /// </summary>
            public List<string> Tags { get; set; } = new();
        }
    }
}