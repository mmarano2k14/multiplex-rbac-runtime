using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Scheduling
{
    /// <summary>
    /// Integration tests for config-driven distributed DAG batch execution.
    /// </summary>
    public sealed class AiDagDistributedBatchExecutionIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";

        [Fact]
        public async Task ExecuteBatchAsync_Should_Execute_Independent_Dag_Steps_In_Bounded_Batch()
        {
            var pipelineName = $"dag-batch-execution-{Guid.NewGuid():N}";
            var jsonPath = await CreatePipelineJsonAsync(pipelineName);

            try
            {
                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    CreateOptions(jsonPath),
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(
                    pipelineName,
                    "hello");

                await host.Engine.ExecuteBatchAsync(
                    created.ExecutionId,
                    maxSteps: 2);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var stateAfterFirstBatch = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(stateAfterFirstBatch);
                Assert.True(stateAfterFirstBatch!.Steps["step-A"].IsCompleted);
                Assert.True(stateAfterFirstBatch.Steps["step-B"].IsCompleted);
                Assert.False(stateAfterFirstBatch.Steps["step-C"].IsCompleted);

                var finalRecord = await host.Engine.ExecuteBatchAsync(
                    created.ExecutionId,
                    maxSteps: 1);

                var stateAfterSecondBatch = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(stateAfterSecondBatch);
                Assert.True(stateAfterSecondBatch!.Steps["step-A"].IsCompleted);
                Assert.True(stateAfterSecondBatch.Steps["step-B"].IsCompleted);
                Assert.True(stateAfterSecondBatch.Steps["step-C"].IsCompleted);
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        [Fact]
        public async Task ExecuteNextAsync_Should_Keep_Single_Step_Distributed_Behavior()
        {
            var pipelineName = $"dag-batch-execution-next-compat-{Guid.NewGuid():N}";
            var jsonPath = await CreatePipelineJsonAsync(pipelineName);

            try
            {
                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    CreateOptions(jsonPath),
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(
                    pipelineName,
                    "hello");

                await host.Engine.ExecuteNextAsync(
                    created.ExecutionId);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var state = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(state);

                var completedCount = state!.Steps.Values.Count(x => x.IsCompleted);

                Assert.Equal(1, completedCount);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        private static AiEngineOptions CreateOptions(
            string jsonPipelineDefinitionFilePath)
        {
            return new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "Json",
                JsonPipelineDefinitionFilePath = jsonPipelineDefinitionFilePath,

                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = false
                },

                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                }
            };
        }

        private static async Task<string> CreatePipelineJsonAsync(
            string pipelineName)
        {
            var fileName = $"{pipelineName}.json";
            var relativePath = Path.Combine("config", fileName);
            var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var json = $$"""
            {
              "pipelines": [
                {
                  "name": "{{pipelineName}}",
                  "version": "1",
                  "executionMode": "Dag",
                  "parallelExecution": {
                    "enabled": true,
                    "maxDegreeOfParallelism": 2
                  },
                  "steps": [
                    {
                      "name": "step-A",
                      "stepKey": "hello-world",
                      "order": 1,
                      "dependsOn": [],
                      "config": { "delayMs": 50 }
                    },
                    {
                      "name": "step-B",
                      "stepKey": "hello-world",
                      "order": 2,
                      "dependsOn": [],
                      "config": { "delayMs": 50 }
                    },
                    {
                      "name": "step-C",
                      "stepKey": "hello-world",
                      "order": 3,
                      "dependsOn": [ "step-A", "step-B" ],
                      "config": { "delayMs": 50 }
                    }
                  ]
                }
              ]
            }
            """;

            await File.WriteAllTextAsync(fullPath, json);

            return relativePath;
        }

        private static void DeletePipelineJson(
            string jsonPipelineDefinitionFilePath)
        {
            var fullPath = Path.Combine(
                AppContext.BaseDirectory,
                jsonPipelineDefinitionFilePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}