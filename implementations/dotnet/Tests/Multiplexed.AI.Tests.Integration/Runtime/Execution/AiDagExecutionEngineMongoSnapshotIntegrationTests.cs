using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System.Numerics;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Verifies that the DAG execution engine persists terminal snapshots
    /// through the real MongoDB snapshot store when snapshot persistence is enabled.
    /// </summary>
    public sealed class AiDagExecutionEngineMongoSnapshotIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";
        private const string CollectionName = "ai_execution_engine_snapshots_tests";

        [Fact]
        public async Task ExecuteNextAsync_Should_Persist_Terminal_Snapshot_To_Mongo_When_Enabled()
        {
            var options = new AiEngineOptions
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

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-complex", "hello");

            AiExecutionRecord result = null!;

            for (var i = 0; i < 50; i++)
            {
                result = await engine.ExecuteNextAsync(created.ExecutionId);

                if (result.IsTerminal)
                {
                    break;
                }
            }

            Assert.NotNull(result);
            Assert.True(result.IsTerminal);

            var snapshotStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();

            var snapshot = await snapshotStore.GetAsync(result.ExecutionId);

            Assert.NotNull(snapshot);
            Assert.Equal(result.ExecutionId, snapshot!.ExecutionId);
            Assert.Equal(result.PipelineName, snapshot.PipelineName);
            Assert.Equal(result.Status.ToString(), snapshot.Status);
            Assert.Equal(result.ContextKey, snapshot.ContextKey);
            Assert.NotNull(snapshot.Record);
            Assert.NotNull(snapshot.State);
            Assert.NotEmpty(snapshot.Steps);
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Not_Persist_Snapshot_When_Disabled()
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\dag-complex-10-steps.json",
                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = false,
                    Mongo = new AiExecutionSnapshotMongoOptions
                    {
                        Enabled = false,
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

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-complex", "hello");
            var result = await engine.ExecuteAllAsync(created.ExecutionId);

            Assert.True(result.IsTerminal);

            if(options.Snapshots.Enabled && options.Snapshots.Mongo.Enabled)
            {
                var snapshotStore =
                host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();
                var snapshot = await snapshotStore.GetAsync(result.ExecutionId);

                Assert.Null(snapshot);
            }
        }
    }
}