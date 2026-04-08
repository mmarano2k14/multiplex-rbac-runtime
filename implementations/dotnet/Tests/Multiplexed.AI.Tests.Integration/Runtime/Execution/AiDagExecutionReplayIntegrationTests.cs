using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Verifies that a persisted execution snapshot can be replayed back into the runtime
    /// and that the restored execution can be resumed safely.
    /// </summary>
    public sealed class AiDagExecutionReplayIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";
        private const string CollectionName = "ai_execution_engine_snapshots_tests";

        [Fact]
        public async Task ReplayAsync_Should_Restore_Execution_From_Snapshot()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            Assert.NotNull(terminal);
            Assert.True(terminal.IsTerminal);

            var snapshotStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

            var snapshot = await snapshotStore.GetAsync(terminal.ExecutionId);

            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot!.Record);
            Assert.NotNull(snapshot.State);

            var executionStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionStore>();

            await executionStore.DeleteRecordAsync(terminal.ExecutionId);
            await executionStore.DeleteStateAsync(terminal.ExecutionId);

            var deletedRecord = await executionStore.GetRecordAsync(terminal.ExecutionId);
            Assert.Null(deletedRecord);

            var replayService =
                host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var replayResult = await replayService.ReplayAsync(terminal.ExecutionId);

            Assert.NotNull(replayResult);
            Assert.True(replayResult.SnapshotFound);
            Assert.True(replayResult.IsValid);
            Assert.True(replayResult.Restored);
            Assert.False(replayResult.AlreadyExists);

            var restoredRecord = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var restoredState = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.NotNull(restoredRecord);
            Assert.NotNull(restoredState);
            Assert.Equal(terminal.ExecutionId, restoredRecord!.ExecutionId);
            Assert.Equal(terminal.ExecutionId, restoredState!.ExecutionId);
        }

        [Fact]
        public async Task ReplayAsync_Should_Return_NotFound_When_Snapshot_Does_Not_Exist()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var replayService =
                host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var executionId = Guid.NewGuid().ToString("N");

            var result = await replayService.ReplayAsync(executionId);

            Assert.NotNull(result);
            Assert.Equal(executionId, result.ExecutionId);
            Assert.False(result.SnapshotFound);
            Assert.False(result.IsValid);
            Assert.False(result.Restored);
        }

        [Fact]
        public async Task ReplayAsync_Should_Restore_Execution_And_Allow_Resume_To_Final_Convergence()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            Assert.NotNull(terminal);
            Assert.True(terminal.IsTerminal);

            var executionStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionStore>();

            await executionStore.DeleteRecordAsync(terminal.ExecutionId);
            await executionStore.DeleteStateAsync(terminal.ExecutionId);

            var replayService =
                host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var replayResult = await replayService.ReplayAsync(terminal.ExecutionId);

            Assert.True(replayResult.Restored);

            var resumed = await engine.ExecuteAllAsync(terminal.ExecutionId);

            Assert.NotNull(resumed);
            Assert.True(resumed.IsTerminal);
            Assert.Equal(terminal.ExecutionId, resumed.ExecutionId);
            Assert.Equal(terminal.Status, resumed.Status);
        }

        private static AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\dag-complex-10-steps.json",
                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = true,
                    Mongo = new AiExecutionSnapshotMongoOptions
                    {
                        Enabled = true,
                        CollectionName = CollectionName
                    }
                },
                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                }
            };
        }

        private static async Task<AiExecutionRecord> ExecuteUntilTerminalAsync(
            AiDagExecutionEngine engine,
            string executionId)
        {
            AiExecutionRecord result = null!;

            for (var i = 0; i < 50; i++)
            {
                result = await engine.ExecuteNextAsync(executionId);

                if (result.IsTerminal)
                {
                    return result;
                }
            }

            throw new InvalidOperationException(
                $"Execution '{executionId}' did not reach a terminal state within the expected iteration budget.");
        }
    }
}