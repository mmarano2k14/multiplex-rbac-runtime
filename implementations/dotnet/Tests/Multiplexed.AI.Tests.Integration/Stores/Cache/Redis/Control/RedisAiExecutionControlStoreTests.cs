using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution.Control;
using Multiplexed.AI.Stores.Cache.Redis.Control;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Stores.Cache.Redis.Control
{
    /// <summary>
    /// Integration tests for <see cref="RedisAiExecutionControlStore"/>.
    /// </summary>
    public sealed class RedisAiExecutionControlStoreTests : IAsyncLifetime
    {
        private readonly string _redisConnectionString = "localhost:6379";
        private IConnectionMultiplexer _multiplexer = default!;
        private RedisAiExecutionControlStore _store = default!;

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString)
                .ConfigureAwait(false);

            _store = new RedisAiExecutionControlStore(
                _multiplexer,
                new RedisExecutionControlKeyBuilder());
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            await _multiplexer.CloseAsync().ConfigureAwait(false);
            _multiplexer.Dispose();
        }

        /// <summary>
        /// Verifies that a stored execution control state can be retrieved from Redis.
        /// </summary>
        [Fact]
        public async Task SetAsync_ThenGetAsync_ShouldPersistExecutionControlState()
        {
            var executionId = CreateExecutionId();

            var state = new AiExecutionControlState
            {
                ExecutionId = executionId,
                Status = AiExecutionControlStatus.Pausing,
                PendingAction = AiExecutionControlAction.Pause,
                Reason = "operator pause",
                RequestedBy = "test",
                PauseRequestedAtUtc = DateTime.UtcNow
            };

            await _store.SetAsync(state).ConfigureAwait(false);

            var loaded = await _store.GetAsync(executionId).ConfigureAwait(false);

            Assert.NotNull(loaded);
            Assert.Equal(executionId, loaded.ExecutionId);
            Assert.Equal(AiExecutionControlStatus.Pausing, loaded.Status);
            Assert.Equal(AiExecutionControlAction.Pause, loaded.PendingAction);
            Assert.Equal("operator pause", loaded.Reason);
            Assert.Equal("test", loaded.RequestedBy);
            Assert.Equal(1, loaded.Version);

            await _store.DeleteAsync(executionId).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that missing control state returns null.
        /// </summary>
        [Fact]
        public async Task GetAsync_WhenStateDoesNotExist_ShouldReturnNull()
        {
            var executionId = CreateExecutionId();

            var loaded = await _store.GetAsync(executionId).ConfigureAwait(false);

            Assert.Null(loaded);
        }

        /// <summary>
        /// Verifies that a versioned update succeeds when the expected version matches.
        /// </summary>
        [Fact]
        public async Task TryUpdateAsync_WhenExpectedVersionMatches_ShouldUpdateState()
        {
            var executionId = CreateExecutionId();

            var initial = new AiExecutionControlState
            {
                ExecutionId = executionId,
                Status = AiExecutionControlStatus.Pausing,
                PendingAction = AiExecutionControlAction.Pause,
                Reason = "pause requested",
                RequestedBy = "test"
            };

            await _store.SetAsync(initial).ConfigureAwait(false);

            var update = new AiExecutionControlState
            {
                ExecutionId = executionId,
                Status = AiExecutionControlStatus.Paused,
                PendingAction = AiExecutionControlAction.None,
                Reason = "pause completed",
                RequestedBy = "worker",
                PausedAtUtc = DateTime.UtcNow
            };

            var updated = await _store.TryUpdateAsync(update, expectedVersion: 1)
                .ConfigureAwait(false);

            var loaded = await _store.GetAsync(executionId).ConfigureAwait(false);

            Assert.True(updated);
            Assert.NotNull(loaded);
            Assert.Equal(AiExecutionControlStatus.Paused, loaded.Status);
            Assert.Equal(AiExecutionControlAction.None, loaded.PendingAction);
            Assert.Equal("pause completed", loaded.Reason);
            Assert.Equal("worker", loaded.RequestedBy);
            Assert.Equal(2, loaded.Version);

            await _store.DeleteAsync(executionId).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that a versioned update fails when the expected version does not match.
        /// </summary>
        [Fact]
        public async Task TryUpdateAsync_WhenExpectedVersionDoesNotMatch_ShouldNotUpdateState()
        {
            var executionId = CreateExecutionId();

            var initial = new AiExecutionControlState
            {
                ExecutionId = executionId,
                Status = AiExecutionControlStatus.Pausing,
                PendingAction = AiExecutionControlAction.Pause,
                Reason = "pause requested",
                RequestedBy = "test"
            };

            await _store.SetAsync(initial).ConfigureAwait(false);

            var staleUpdate = new AiExecutionControlState
            {
                ExecutionId = executionId,
                Status = AiExecutionControlStatus.Cancelled,
                PendingAction = AiExecutionControlAction.Cancel,
                Reason = "stale cancel",
                RequestedBy = "test"
            };

            var updated = await _store.TryUpdateAsync(staleUpdate, expectedVersion: 99)
                .ConfigureAwait(false);

            var loaded = await _store.GetAsync(executionId).ConfigureAwait(false);

            Assert.False(updated);
            Assert.NotNull(loaded);
            Assert.Equal(AiExecutionControlStatus.Pausing, loaded.Status);
            Assert.Equal(AiExecutionControlAction.Pause, loaded.PendingAction);
            Assert.Equal("pause requested", loaded.Reason);
            Assert.Equal(1, loaded.Version);

            await _store.DeleteAsync(executionId).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that deleting a stored control state removes it from Redis.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WhenStateExists_ShouldRemoveState()
        {
            var executionId = CreateExecutionId();

            var state = new AiExecutionControlState
            {
                ExecutionId = executionId,
                Status = AiExecutionControlStatus.Cancelling,
                PendingAction = AiExecutionControlAction.Cancel,
                Reason = "operator cancel",
                RequestedBy = "test",
                CancellationRequestedAtUtc = DateTime.UtcNow
            };

            await _store.SetAsync(state).ConfigureAwait(false);

            var deleted = await _store.DeleteAsync(executionId).ConfigureAwait(false);
            var loaded = await _store.GetAsync(executionId).ConfigureAwait(false);

            Assert.True(deleted);
            Assert.Null(loaded);
        }

        /// <summary>
        /// Verifies that waiting-for-input metadata and input payload are persisted.
        /// </summary>
        [Fact]
        public async Task SetAsync_WithWaitingForInputState_ShouldPersistWaitingMetadataAndInput()
        {
            var executionId = CreateExecutionId();

            var state = new AiExecutionControlState
            {
                ExecutionId = executionId,
                Status = AiExecutionControlStatus.WaitingForInput,
                PendingAction = AiExecutionControlAction.WaitForInput,
                Reason = "approval required",
                RequestedBy = "runtime",
                WaitingKey = "approval:pricing",
                WaitingStepName = "human-approval",
                WaitingStartedAtUtc = DateTime.UtcNow,
                Input = new Dictionary<string, object?>
                {
                    ["approved"] = true,
                    ["comment"] = "approved by test"
                }
            };

            await _store.SetAsync(state).ConfigureAwait(false);

            var loaded = await _store.GetAsync(executionId).ConfigureAwait(false);

            Assert.NotNull(loaded);
            Assert.Equal(AiExecutionControlStatus.WaitingForInput, loaded.Status);
            Assert.Equal(AiExecutionControlAction.WaitForInput, loaded.PendingAction);
            Assert.Equal("approval:pricing", loaded.WaitingKey);
            Assert.Equal("human-approval", loaded.WaitingStepName);
            Assert.True(loaded.Input.ContainsKey("approved"));
            Assert.True(loaded.Input.ContainsKey("comment"));

            await _store.DeleteAsync(executionId).ConfigureAwait(false);
        }

        private static string CreateExecutionId()
        {
            return $"test-exec-control-{Guid.NewGuid():N}";
        }
    }
}