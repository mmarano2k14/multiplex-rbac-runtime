// Fixed version with PayloadStore configuration added

using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay;
using Multiplexed.Abstractions.AI.Execution.Persistence.Replay.Models;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
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
            var executionStore = host.ServiceProvider.GetRequiredService<IAiExecutionStore>();
            var snapshotStore = host.ServiceProvider.GetRequiredService<IAiExecutionSnapshotStore<ExecutionContextSnapshot>>();
            var replayService = host.ServiceProvider.GetRequiredService<IAiExecutionReplayService>();

            var created = await engine.CreateAsync("dag-complex", "hello");
            var terminal = await ExecuteUntilTerminalAsync(engine, created.ExecutionId);

            var snapshot = await snapshotStore.GetAsync(terminal.ExecutionId);

            await executionStore.DeleteRecordAsync(terminal.ExecutionId);
            await executionStore.DeleteStateAsync(terminal.ExecutionId);

            var replayResult = await replayService.ReplayAsync(
                new AiExecutionReplayRequest
                {
                    ExecutionId = terminal.ExecutionId,
                    Mode = AiExecutionReplayMode.ResumeIncomplete
                });

            Assert.True(replayResult.ReplayValid);
            Assert.True(replayResult.ExecutionFound);
            Assert.True(replayResult.SnapshotFound);
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
                PayloadStore = new AiPayloadStoreOptions
                {
                    Enabled = true,
                    Provider = "mongo-redis",
                    MaxInlineSizeBytes = 512,
                    Mongo = new MongoAiPayloadStoreOptions
                    {
                        Enabled = true,
                        ConnectionString = ConnectionString,
                        DatabaseName = DatabaseName,
                        CollectionName = "payloads_replay_tests"
                    },
                    RedisCache = new RedisAiPayloadCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = "test:ai:payload",
                        ExpirationSeconds = 120
                    },
                    StepIndexCache = new RedisAiStepPayloadIndexCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = "test:ai:step-index",
                        ExpirationSeconds = 120
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

            throw new InvalidOperationException("Execution did not reach terminal state.");
        }
    }
}
