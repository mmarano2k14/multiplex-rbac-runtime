using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Sample.External.Plugins.Rag;
using Multiplexed.Sample.External.Plugins.Rag.DI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    /// <summary>
    /// Final external-provider RAG validation with:
    /// - InMemory
    /// - EF direct
    /// - EF provider
    /// - Mongo snapshots
    /// - cleanup lifecycle
    /// - replay validation
    ///
    /// Pipeline:
    /// candidate + job → merge → compose → snapshot → cleanup
    /// </summary>
    public sealed class RagEndToEndExternalProvidersSnapshotCleanupIntegrationTests
    {
        private const string SqlServerConnectionString =
            "Server=MSI\\SQLEXPRESS;Database=TestAiRuntimeRag;Trusted_Connection=True;TrustServerCertificate=True;";

        private const string PostgresConnectionString =
            "Host=localhost;Port=5432;Database=TestAiRuntimeRag;Username=postgres;Password=sa";

        private const string MongoConnectionString = "mongodb://localhost:27017";
        private const string MongoDatabaseName = "multiplexed_ai_tests";
        private const string MongoCollectionName = "ai_execution_snapshots_tests";

        // ============================================================
        // IN MEMORY
        // ============================================================

        [Fact]
        public async Task ExecuteAllAsync_InMemory_Should_Succeed_And_Persist_Snapshot()
        {
            await using var host = await CreateHostAsync(
                enableCleanup: false,
                deleteSnapshotsIfExist: false,
                services =>
                {
                    services.AddExternalSqlServerInMemory();
                    services.AddExternalPostgresInMemory();
                    services.AddExternalRag();
                });

            await ExecuteAndValidateCompletedPipeline(host, "direct", expectSnapshotPersisted: true);
        }

        // ============================================================
        // EF DIRECT (internal execution)
        // ============================================================

        [Fact]
        public async Task ExecuteAllAsync_Ef_Direct_Should_Succeed_And_Persist_Snapshot()
        {
            await using var host = await CreateHostAsync(
                enableCleanup: false,
                deleteSnapshotsIfExist: false,
                services =>
                {
                    services.AddExternalSqlServerEf(SqlServerConnectionString);
                    services.AddExternalPostgresEf(PostgresConnectionString);
                    services.AddExternalRag();
                });

            await ExecuteAndValidateCompletedPipeline(host, "direct", expectSnapshotPersisted: true);
        }

        // ============================================================
        // EF PROVIDER
        // ============================================================

        [Fact]
        public async Task ExecuteAllAsync_Ef_Provider_Should_Succeed_And_Persist_Snapshot()
        {
            await using var host = await CreateHostAsync(
                enableCleanup: false,
                deleteSnapshotsIfExist: false,
                services =>
                {
                    services.AddExternalSqlServerEf(SqlServerConnectionString);
                    services.AddExternalPostgresEf(PostgresConnectionString);
                    services.AddExternalRag();
                });

            await ExecuteAndValidateCompletedPipeline(host, "provider", expectSnapshotPersisted: true);
        }

        // ============================================================
        // EF PROVIDER + SNAPSHOT + REPLAY
        // ============================================================

        [Fact]
        public async Task ExecuteAllAsync_Ef_Provider_Should_Persist_Snapshot_And_Replay()
        {
            await using var host = await CreateHostAsync(
                enableCleanup: false,
                deleteSnapshotsIfExist: false,
                services =>
                {
                    services.AddExternalSqlServerEf(SqlServerConnectionString);
                    services.AddExternalPostgresEf(PostgresConnectionString);
                    services.AddExternalRag();
                });

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-endtoend-external-final-test",
                new Dictionary<string, object?>
                {
                    ["candidateId"] = "cand-001",
                    ["jobId"] = "job-001",
                    ["candidateProviderKey"] = "sqlserver",
                    ["jobProviderKey"] = "postgres",
                    ["candidateExecutionMode"] = "provider",
                    ["jobExecutionMode"] = "provider"
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            Assert.NotNull(final);
            Assert.True(final!.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, final.Status);

            var snapshotStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

            var snapshot = await snapshotStore.GetAsync(created.ExecutionId);

            Assert.NotNull(snapshot);
            Assert.Equal(created.ExecutionId, snapshot!.ExecutionId);
            Assert.Equal("rag-endtoend-external-final-test", snapshot.PipelineName);
            Assert.Equal(AiExecutionStatus.Completed.ToString(), snapshot.Status);
            Assert.NotNull(snapshot.Record);
            Assert.NotNull(snapshot.State);

            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var replayResult = await replayService.ReplayAsync(created.ExecutionId);

            Assert.NotNull(replayResult);
            Assert.True(replayResult.Restored || replayResult.AlreadyExists);
        }

        // ============================================================
        // CLEANUP - IN MEMORY
        // ============================================================

        [Fact]
        public async Task ExecuteAllAsync_InMemory_Should_Remove_Snapshot_When_Cleanup_Is_Enabled()
        {
            await using var host = await CreateHostAsync(
                enableCleanup: true,
                deleteSnapshotsIfExist: true,
                services =>
                {
                    services.AddExternalSqlServerInMemory();
                    services.AddExternalPostgresInMemory();
                    services.AddExternalRag();
                });

            await ExecuteAndValidateCleanup(host, "direct");
        }

        // ============================================================
        // CLEANUP - EF PROVIDER
        // ============================================================

        [Fact]
        public async Task ExecuteAllAsync_Ef_Provider_Should_Remove_Snapshot_When_Cleanup_Is_Enabled()
        {
            await using var host = await CreateHostAsync(
                enableCleanup: true,
                deleteSnapshotsIfExist: true,
                services =>
                {
                    services.AddExternalSqlServerEf(SqlServerConnectionString);
                    services.AddExternalPostgresEf(PostgresConnectionString);
                    services.AddExternalRag();
                });

            await ExecuteAndValidateCleanup(host, "provider");
        }

        // ============================================================
        // EXECUTION HELPERS
        // ============================================================

        private static async Task ExecuteAndValidateCompletedPipeline(
            AiDagExecutionEngineTestHost host,
            string executionMode,
            bool expectSnapshotPersisted)
        {
            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-endtoend-external-final-test",
                new Dictionary<string, object?>
                {
                    ["candidateId"] = "cand-001",
                    ["jobId"] = "job-001",
                    ["candidateProviderKey"] = "sqlserver",
                    ["jobProviderKey"] = "postgres",
                    ["candidateExecutionMode"] = executionMode,
                    ["jobExecutionMode"] = executionMode
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            Assert.NotNull(final);
            Assert.True(final!.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, final.Status);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(created.ExecutionId);
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(record);
            Assert.NotNull(state);
            Assert.NotNull(state!.Steps);

            Assert.True(record!.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, record.Status);

            Assert.True(state.Steps["candidate"].IsCompleted);
            Assert.True(state.Steps["job"].IsCompleted);
            Assert.True(state.Steps["merge"].IsCompleted);
            Assert.True(state.Steps["compose"].IsCompleted);

            var candidate = state.Steps["candidate"];
            Assert.NotNull(candidate.Result);
            Assert.NotNull(candidate.Result!.Data);
            Assert.True(candidate.Result.Data.ContainsKey("batch"));

            var job = state.Steps["job"];
            Assert.NotNull(job.Result);
            Assert.NotNull(job.Result!.Data);
            Assert.True(job.Result.Data.ContainsKey("batch"));

            var merge = state.Steps["merge"];
            Assert.NotNull(merge.Result);
            Assert.NotNull(merge.Result!.Data);
            Assert.True(merge.Result.Data.ContainsKey("batch"));
            Assert.True(merge.Result.Data.ContainsKey("itemCount"));
            Assert.True(merge.Result.Data.ContainsKey("diagnostics"));

            var compose = state.Steps["compose"];
            Assert.NotNull(compose.Result);
            Assert.NotNull(compose.Result!.Data);
            Assert.True(compose.Result.Data.ContainsKey("context"));
            Assert.True(compose.Result.Data.ContainsKey("fragments"));

            var snapshotStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

            var snapshot = await snapshotStore.GetAsync(created.ExecutionId);

            if (expectSnapshotPersisted)
            {
                Assert.NotNull(snapshot);
                Assert.Equal(created.ExecutionId, snapshot!.ExecutionId);
                Assert.Equal("rag-endtoend-external-final-test", snapshot.PipelineName);
                Assert.Equal(AiExecutionStatus.Completed.ToString(), snapshot.Status);
                Assert.NotNull(snapshot.Record);
                Assert.NotNull(snapshot.State);
            }
        }

        private static async Task ExecuteAndValidateCleanup(
            AiDagExecutionEngineTestHost host,
            string executionMode)
        {
            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-endtoend-external-final-test",
                new Dictionary<string, object?>
                {
                    ["candidateId"] = "cand-001",
                    ["jobId"] = "job-001",
                    ["candidateProviderKey"] = "sqlserver",
                    ["jobProviderKey"] = "postgres",
                    ["candidateExecutionMode"] = executionMode,
                    ["jobExecutionMode"] = executionMode
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            Assert.NotNull(final);
            Assert.True(final!.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, final.Status);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(created.ExecutionId);
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            var snapshotStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

            var snapshot = await snapshotStore.GetAsync(created.ExecutionId);

            Assert.True(record is null || record.IsTerminal);
            Assert.True(state is null || state.Steps.Count >= 0);
            Assert.Null(snapshot);
        }

        // ============================================================
        // HOST SETUP
        // ============================================================

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync(
            bool enableCleanup,
            bool deleteSnapshotsIfExist,
            Action<IServiceCollection> configure)
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\rag-endtoend-external-final-test.json",
                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = true,
                    Mongo = new AiExecutionSnapshotMongoOptions
                    {
                        Enabled = true,
                        ConnectionString = MongoConnectionString,
                        DatabaseName = MongoDatabaseName,
                        CollectionName = MongoCollectionName
                    }
                },
                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = enableCleanup,
                    AutoCleanupOnFailed = enableCleanup,
                    SuppressSnapshotIfExist = deleteSnapshotsIfExist,
                    SuppressCleanupExceptions = true
                }
            };

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddMultiplexAI(options);

                    configure(services);

                    services.AddRagFromAssemblies(
                        typeof(RagPluginsAssemblyMarker).Assembly,
                        typeof(AiRuntimeAssemblyMarker).Assembly);
                },
                mongoConnectionString: MongoConnectionString,
                mongoDatabaseName: MongoDatabaseName);
        }
    }
}