using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Verifies snapshot replay behavior against the real integration fixture.
    ///
    /// These tests validate that:
    /// - persisted snapshots can be restored into the runtime store
    /// - replay is safe when the snapshot is missing
    /// - replayed executions can resume and converge correctly
    /// - restore writes record and state back together
    /// - replay is idempotent when a compatible execution already exists
    ///
    /// IMPORTANT:
    /// - In distributed DAG mode, authoritative execution truth lives in <see cref="IAiDagExecutionStore"/>
    /// - The generic <see cref="IAiExecutionStore"/> remains relevant for the replay/restore contract itself
    /// - Therefore these tests intentionally use both stores depending on what is being verified
    /// </summary>
    public sealed class AiDagExecutionReplayIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";
        private const string CollectionName = "ai_execution_engine_snapshots_tests";

        /// <summary>
        /// Verifies that a persisted snapshot can be replayed back into the runtime store
        /// after the generic runtime record and state have been removed.
        ///
        /// WHAT THIS TEST PROVES:
        /// - snapshot lookup succeeds
        /// - replay restores the runtime store contract
        /// - replay reports a successful restore result
        ///
        /// IMPORTANT:
        /// - This test targets the replay service contract against the generic runtime store
        /// - It does not assert distributed DAG truth yet; it only verifies restore behavior
        /// </summary>
        [Fact]
        public async Task ReplayAsync_Should_Restore_Execution_From_Snapshot()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;
            var executionStore = host.ServiceProvider.GetRequiredService<IAiExecutionStore>();
            var snapshotStore = host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();
            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            Assert.NotNull(terminal);
            Assert.True(terminal.IsTerminal);

            var snapshot = await snapshotStore.GetAsync(terminal.ExecutionId);

            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot!.Record);
            Assert.NotNull(snapshot.State);

            await executionStore.DeleteRecordAsync(terminal.ExecutionId);
            await executionStore.DeleteStateAsync(terminal.ExecutionId);

            var deletedRecord = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var deletedState = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.Null(deletedRecord);
            Assert.Null(deletedState);

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

        /// <summary>
        /// Verifies that replay returns a non-restored result when no snapshot exists
        /// for the requested execution identifier.
        /// </summary>
        [Fact]
        public async Task ReplayAsync_Should_Return_NotFound_When_Snapshot_Does_Not_Exist()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();
            var executionId = Guid.NewGuid().ToString("N");

            var result = await replayService.ReplayAsync(executionId);

            Assert.NotNull(result);
            Assert.Equal(executionId, result.ExecutionId);
            Assert.False(result.SnapshotFound);
            Assert.False(result.IsValid);
            Assert.False(result.Restored);
            Assert.False(result.AlreadyExists);
        }

        /// <summary>
        /// Verifies that a replayed execution can be resumed by the engine
        /// and still converge to the expected final terminal state.
        ///
        /// WHAT THIS TEST PROVES:
        /// - replay restores enough runtime data for execution to resume
        /// - the engine can continue from replayed state
        /// - the authoritative distributed DAG truth converges back to terminal state
        /// </summary>
        [Fact]
        public async Task ReplayAsync_Should_Restore_Execution_And_Allow_Resume_To_Final_Convergence()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;
            var executionStore = host.ServiceProvider.GetRequiredService<IAiExecutionStore>();
            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            Assert.NotNull(terminal);
            Assert.True(terminal.IsTerminal);

            // Remove the generic runtime projection so replay has something to restore.
            await executionStore.DeleteRecordAsync(terminal.ExecutionId);
            await executionStore.DeleteStateAsync(terminal.ExecutionId);

            var replayResult = await replayService.ReplayAsync(terminal.ExecutionId);

            Assert.True(replayResult.Restored);

            var resumed = await engine.ExecuteAllAsync(terminal.ExecutionId);

            Assert.NotNull(resumed);

            // Read final authoritative truth from the distributed DAG store.
            var (finalRecord, finalState) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                terminal.ExecutionId);

            Assert.True(finalRecord.IsTerminal);
            Assert.Equal(terminal.ExecutionId, finalRecord.ExecutionId);
            Assert.Equal(terminal.Status, finalRecord.Status);

            Assert.NotNull(finalState.Steps);
            Assert.NotEmpty(finalState.Steps);
        }

        /// <summary>
        /// Verifies that restore writes both record and state back into the runtime store
        /// and that the restored data remains internally consistent.
        ///
        /// WHAT THIS TEST PROVES:
        /// - restore writes record and state together
        /// - restored record/state share the same execution identity
        /// - restored payload remains structurally coherent
        ///
        /// IMPORTANT:
        /// - This test validates the observable restore contract against <see cref="IAiExecutionStore"/>
        /// - It is intentionally separate from distributed convergence assertions
        /// </summary>
        [Fact]
        public async Task RestoreAsync_Should_Restore_Record_And_State_Atomically()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;
            var executionStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var snapshotStore = host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            Assert.NotNull(terminal);
            Assert.True(terminal.IsTerminal);

            var snapshot = await snapshotStore.GetAsync(terminal.ExecutionId);

            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot!.Record);
            Assert.NotNull(snapshot.State);

            await executionStore.DeleteExecutionBundleAsync(terminal.ExecutionId);

            var deletedRecord = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var deletedState = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.Null(deletedRecord);
            Assert.Null(deletedState);

            AiExecutionReplayPreparation.Prepare(snapshot.Record, snapshot.State);

            await executionStore.RestoreAsync(snapshot.Record, snapshot.State);

            var restoredRecord = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var restoredState = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.NotNull(restoredRecord);
            Assert.NotNull(restoredState);

            Assert.Equal(terminal.ExecutionId, restoredRecord!.ExecutionId);
            Assert.Equal(terminal.ExecutionId, restoredState!.ExecutionId);
            Assert.Equal(restoredRecord.ExecutionId, restoredState.ExecutionId);
            Assert.Equal(snapshot.Record!.PipelineName, restoredRecord.PipelineName);
            Assert.Equal(snapshot.Record.ContextKey, restoredRecord.ContextKey);

            Assert.NotNull(restoredState.Steps);
            Assert.NotEmpty(restoredState.Steps);
        }

        /// <summary>
        /// Verifies idempotent replay behavior.
        ///
        /// If a compatible execution already exists in the runtime store,
        /// replay must not restore over it again. Instead, replay should report
        /// that the execution already exists and that no restore was performed.
        ///
        /// IMPORTANT:
        /// - This test targets replay idempotence against the generic runtime store contract
        /// - It verifies that replay skips a compatible existing runtime projection
        /// </summary>
        [Fact]
        public async Task ReplayAsync_Should_Not_Restore_When_Compatible_Execution_Already_Exists()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;
            var executionStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            Assert.NotNull(terminal);
            Assert.True(terminal.IsTerminal);

            var existingRecordBeforeReplay = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var existingStateBeforeReplay = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.NotNull(existingRecordBeforeReplay);
            Assert.NotNull(existingStateBeforeReplay);

            var replayResult = await replayService.ReplayAsync(terminal.ExecutionId);

            Assert.NotNull(replayResult);
            Assert.True(replayResult.SnapshotFound);
            Assert.True(replayResult.IsValid);
            Assert.True(replayResult.AlreadyExists);
            Assert.False(replayResult.Restored);
            Assert.Equal(existingRecordBeforeReplay!.Status, replayResult.ExistingStatus);

            var existingRecordAfterReplay = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var existingStateAfterReplay = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.NotNull(existingRecordAfterReplay);
            Assert.NotNull(existingStateAfterReplay);

            Assert.Equal(existingRecordBeforeReplay.ExecutionId, existingRecordAfterReplay!.ExecutionId);
            Assert.Equal(existingRecordBeforeReplay.PipelineName, existingRecordAfterReplay.PipelineName);
            Assert.Equal(existingRecordBeforeReplay.ContextKey, existingRecordAfterReplay.ContextKey);

            Assert.Equal(existingStateBeforeReplay!.ExecutionId, existingStateAfterReplay!.ExecutionId);
            Assert.NotNull(existingStateAfterReplay.Steps);
            Assert.NotEmpty(existingStateAfterReplay.Steps);
        }

        /// <summary>
        /// Verifies that replay does not skip restore when an execution already exists
        /// in the runtime store but is not compatible with the persisted snapshot.
        ///
        /// This protects the idempotence logic from becoming too permissive:
        /// a replay must only be skipped when the existing runtime execution
        /// truly matches the snapshot identity and logical context.
        /// </summary>
        [Fact]
        public async Task ReplayAsync_Should_Restore_When_Existing_Execution_Is_Not_Compatible()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;
            var executionStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            Assert.NotNull(terminal);
            Assert.True(terminal.IsTerminal);

            var existingRecord = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var existingState = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.NotNull(existingRecord);
            Assert.NotNull(existingState);

            // Make the existing runtime execution incompatible with the snapshot.
            // ContextKey is part of the replay compatibility check.
            existingRecord!.ContextKey = "incompatible-context-key";

            await executionStore.SaveRecordAsync(existingRecord);

            var replayResult = await replayService.ReplayAsync(terminal.ExecutionId);

            Assert.NotNull(replayResult);
            Assert.True(replayResult.SnapshotFound);
            Assert.True(replayResult.IsValid);
            Assert.False(replayResult.AlreadyExists);
            Assert.True(replayResult.Restored);

            var restoredRecord = await executionStore.GetRecordAsync(terminal.ExecutionId);
            var restoredState = await executionStore.GetStateAsync(terminal.ExecutionId);

            Assert.NotNull(restoredRecord);
            Assert.NotNull(restoredState);

            Assert.Equal(terminal.ExecutionId, restoredRecord!.ExecutionId);
            Assert.Equal(terminal.PipelineName, restoredRecord.PipelineName);
            Assert.Equal(terminal.ContextKey, restoredRecord.ContextKey);
        }

        /// <summary>
        /// Creates the integration test engine options used by replay tests.
        /// </summary>
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

        /// <summary>
        /// Executes the DAG engine until a terminal state is reached
        /// or until the test iteration budget is exhausted.
        ///
        /// IMPORTANT:
        /// - The returned record is the engine return value for the current attempt
        /// - Final authoritative truth for distributed DAG assertions should still be read
        ///   through <see cref="IAiDagExecutionStore"/>
        /// </summary>
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

        /// <summary>
        /// Loads the authoritative distributed record and state from the DAG store.
        ///
        /// IMPORTANT:
        /// - In distributed DAG mode, <see cref="IAiDagExecutionStore"/> is the source of truth
        /// - Use this helper for assertions about final execution truth after the engine runs
        /// </summary>
        private static async Task<(AiExecutionRecord Record, AiExecutionState State)> LoadDistributedTruthAsync(
            IServiceProvider services,
            string executionId)
        {
            var dagStore = services.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(executionId);
            var state = await dagStore.GetStateAsync(executionId);

            Assert.NotNull(record);
            Assert.NotNull(state);

            return (record!, state!);
        }
    }
}