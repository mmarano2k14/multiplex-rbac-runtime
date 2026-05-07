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
    /// Integration tests for distributed DAG batch execution with dynamically generated pipelines.
    /// </summary>
    public sealed class AiDagDistributedBatchExecutionLargeDagIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";
        private const int StepCount = 50;

        [Fact]
        public async Task ExecuteBatchAsync_Should_Complete_50_Step_Dag_With_Bounded_Parallelism()
        {
            var pipelineName = $"dag-batch-50-{Guid.NewGuid():N}";
            var jsonPath = await CreatePipelineJsonAsync(pipelineName, StepCount);

            try
            {
                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    CreateOptions(jsonPath),
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(pipelineName, "hello");

                AiExecutionRecord record;

                do
                {
                    record = await host.Engine
                        .ExecuteBatchAsync(created.ExecutionId, maxSteps: 10)
                        .WaitAsync(TimeSpan.FromSeconds(30));
                }
                while (!record.IsTerminal);

                Assert.Equal(AiExecutionStatus.Completed, record.Status);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var finalState = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(finalState);
                Assert.Equal(StepCount, finalState!.Steps.Count);
                Assert.All(finalState.Steps.Values, step => Assert.True(step.IsCompleted));
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        [Fact]
        public async Task Concurrent_ExecuteBatchAsync_Should_Complete_50_Step_Dag_Without_Double_Claiming()
        {
            var pipelineName = $"dag-batch-50-concurrent-{Guid.NewGuid():N}";
            var jsonPath = await CreatePipelineJsonAsync(pipelineName, StepCount);

            try
            {
                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    CreateOptions(jsonPath),
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(pipelineName, "hello");

                var workers = Enumerable.Range(0, 4)
                    .Select(_ => Task.Run(async () =>
                    {
                        AiExecutionRecord record;

                        do
                        {
                            record = await host.Engine
                                .ExecuteBatchAsync(created.ExecutionId, maxSteps: 10)
                                .WaitAsync(TimeSpan.FromSeconds(30));
                        }
                        while (!record.IsTerminal && record.Status != AiExecutionStatus.Waiting);

                        return record;
                    }))
                    .ToArray();

                var records = await Task.WhenAll(workers);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                AiExecutionRecord? finalRecord = null;

                for (var i = 0; i < 10; i++)
                {
                    finalRecord = await dagStore.GetRecordAsync(
                        created.ExecutionId,
                        CancellationToken.None);

                    if (finalRecord?.IsTerminal == true)
                    {
                        break;
                    }

                    await host.Engine.ExecuteBatchAsync(created.ExecutionId, maxSteps: 10);
                }

                Assert.NotNull(finalRecord);
                Assert.True(finalRecord!.IsTerminal);
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);

                var finalState = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(finalState);
                Assert.Equal(StepCount, finalState!.Steps.Count);
                Assert.All(finalState.Steps.Values, step => Assert.True(step.IsCompleted));
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Creates test runtime options using the JSON pipeline definition source.
        /// </summary>
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

        /// <summary>
        /// Creates a temporary dynamically generated DAG pipeline JSON file.
        /// </summary>
        private static async Task<string> CreatePipelineJsonAsync(
            string pipelineName,
            int stepCount)
        {
            var fileName = $"{pipelineName}.json";
            var relativePath = Path.Combine("config", fileName);
            var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var stepsJson = GenerateStepsJson(stepCount);

            var json = $$"""
            {
              "pipelines": [
                {
                  "name": "{{pipelineName}}",
                  "version": "1",
                  "executionMode": "Dag",
                  "parallelExecution": {
                    "enabled": true,
                    "maxDegreeOfParallelism": 8
                  },
                  "steps": [
            {{stepsJson}}
                  ]
                }
              ]
            }
            """;

            await File.WriteAllTextAsync(fullPath, json);

            return relativePath;
        }

        /// <summary>
        /// Generates a deterministic 50-step DAG with mixed dependency levels.
        /// </summary>
        private static string GenerateStepsJson(int stepCount)
        {
            var steps = new List<string>();

            for (var i = 1; i <= stepCount; i++)
            {
                var stepName = $"step-{i:00}";
                var dependsOn = GetDependencies(i);

                var dependsOnJson = dependsOn.Count == 0
                    ? "[]"
                    : "[ " + string.Join(", ", dependsOn.Select(x => $"\"{x}\"")) + " ]";

                var comma = i == stepCount ? string.Empty : ",";

                steps.Add($$"""
                    {
                      "name": "{{stepName}}",
                      "stepKey": "hello-world",
                      "order": {{i}},
                      "dependsOn": {{dependsOnJson}},
                      "config": { "delayMs": 10 }
                    }{{comma}}
            """);
            }

            return string.Join(Environment.NewLine, steps);
        }

        /// <summary>
        /// Returns deterministic dependencies for the generated DAG.
        /// </summary>
        private static IReadOnlyList<string> GetDependencies(int stepNumber)
        {
            if (stepNumber <= 10)
            {
                return Array.Empty<string>();
            }

            if (stepNumber <= 25)
            {
                return new[]
                {
                    $"step-{((stepNumber - 11) % 10) + 1:00}",
                    $"step-{((stepNumber - 7) % 10) + 1:00}"
                };
            }

            if (stepNumber <= 40)
            {
                return new[]
                {
                    $"step-{((stepNumber - 26) % 15) + 11:00}",
                    $"step-{((stepNumber - 22) % 15) + 11:00}"
                };
            }

            return new[]
            {
                $"step-{((stepNumber - 41) % 15) + 26:00}",
                $"step-{((stepNumber - 37) % 15) + 26:00}"
            };
        }

        /// <summary>
        /// Deletes a temporary JSON pipeline definition when it exists.
        /// </summary>
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