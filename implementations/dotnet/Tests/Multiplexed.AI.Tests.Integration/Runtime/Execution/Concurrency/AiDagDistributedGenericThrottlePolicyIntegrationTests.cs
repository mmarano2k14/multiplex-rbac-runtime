using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Concurrency
{
    /// <summary>
    /// Integration tests for pipeline-level generic concurrency throttle policies.
    /// </summary>
    /// <remarks>
    /// These tests validate the full runtime path:
    /// JSON pipeline configuration, generic throttle policy resolution,
    /// targeted throttle rule matching, Redis distributed admission,
    /// release after execution, and deterministic DAG convergence.
    /// </remarks>
    public sealed class AiDagDistributedGenericThrottlePolicyIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";

        /// <summary>
        /// Verifies that a pipeline-level generic provider throttle limits matching OpenAI steps.
        /// </summary>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Throttle_OpenAi_Provider_From_Pipeline_Generic_Throttle_Policy()
        {
            var pipelineName = $"dag-generic-provider-throttle-{Guid.NewGuid():N}";
            var jsonPath = await CreateGenericThrottlePipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-4.1",
                operation: "llm.chat",
                policyConfigJson: """
                {
                  "scope": "provider",
                  "target": "openai",
                  "limit": 1,
                  "leaseSeconds": 300,
                  "defaultRetryAfterMs": 100
                }
                """);

            try
            {
                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    CreateOptions(jsonPath),
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(pipelineName, "hello");

                var firstBatch = await host.Engine
                    .ExecuteBatchAsync(created.ExecutionId, maxSteps: 10)
                    .WaitAsync(TimeSpan.FromSeconds(30));

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var stateAfterFirstBatch = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(stateAfterFirstBatch);

                var completedAfterFirstBatch = stateAfterFirstBatch!.Steps.Values
                    .Count(step => step.Status == AiStepExecutionStatus.Completed || step.IsCompleted);

                Assert.Equal(1, completedAfterFirstBatch);
                Assert.NotEqual(AiExecutionStatus.Completed, firstBatch.Status);

                AiExecutionRecord record;

                do
                {
                    record = await host.Engine
                        .ExecuteBatchAsync(created.ExecutionId, maxSteps: 10)
                        .WaitAsync(TimeSpan.FromSeconds(30));
                }
                while (!record.IsTerminal);

                Assert.Equal(AiExecutionStatus.Completed, record.Status);

                var finalState = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(finalState);
                Assert.Equal(2, finalState!.Steps.Count);
                Assert.All(
                    finalState.Steps.Values,
                    step =>
                    {
                        Assert.True(step.IsCompleted);
                        Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
                    });
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that a pipeline-level provider throttle is ignored when the target does not match.
        /// </summary>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Not_Apply_Generic_Provider_Throttle_When_Target_Does_Not_Match()
        {
            var pipelineName = $"dag-generic-provider-throttle-no-match-{Guid.NewGuid():N}";
            var jsonPath = await CreateGenericThrottlePipelineJsonAsync(
                pipelineName,
                provider: "anthropic",
                model: "claude-sonnet",
                operation: "llm.chat",
                policyConfigJson: """
                {
                  "scope": "provider",
                  "target": "openai",
                  "limit": 1,
                  "leaseSeconds": 300,
                  "defaultRetryAfterMs": 100
                }
                """);

            try
            {
                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    CreateOptions(jsonPath),
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(pipelineName, "hello");

                var record = await host.Engine
                    .ExecuteBatchAsync(created.ExecutionId, maxSteps: 10)
                    .WaitAsync(TimeSpan.FromSeconds(30));

                Assert.Equal(AiExecutionStatus.Completed, record.Status);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var finalState = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(finalState);
                Assert.Equal(2, finalState!.Steps.Count);
                Assert.All(
                    finalState.Steps.Values,
                    step =>
                    {
                        Assert.True(step.IsCompleted);
                        Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
                    });
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that a pipeline-level generic model throttle limits matching provider/model steps.
        /// </summary>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Throttle_Model_From_Pipeline_Generic_Throttle_Policy()
        {
            var pipelineName = $"dag-generic-model-throttle-{Guid.NewGuid():N}";
            var jsonPath = await CreateGenericThrottlePipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-4.1",
                operation: "llm.chat",
                policyConfigJson: """
                {
                  "scope": "model",
                  "target": "openai:gpt-4.1",
                  "limit": 1,
                  "leaseSeconds": 300,
                  "defaultRetryAfterMs": 100
                }
                """);

            try
            {
                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    CreateOptions(jsonPath),
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(pipelineName, "hello");

                var firstBatch = await host.Engine
                    .ExecuteBatchAsync(created.ExecutionId, maxSteps: 10)
                    .WaitAsync(TimeSpan.FromSeconds(30));

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var stateAfterFirstBatch = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(stateAfterFirstBatch);

                var completedAfterFirstBatch = stateAfterFirstBatch!.Steps.Values
                    .Count(step => step.Status == AiStepExecutionStatus.Completed || step.IsCompleted);

                Assert.Equal(1, completedAfterFirstBatch);
                Assert.NotEqual(AiExecutionStatus.Completed, firstBatch.Status);

                AiExecutionRecord record;

                do
                {
                    record = await host.Engine
                        .ExecuteBatchAsync(created.ExecutionId, maxSteps: 10)
                        .WaitAsync(TimeSpan.FromSeconds(30));
                }
                while (!record.IsTerminal);

                Assert.Equal(AiExecutionStatus.Completed, record.Status);
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
        /// Creates a temporary two-step DAG pipeline using a pipeline-level generic throttle policy.
        /// </summary>
        private static async Task<string> CreateGenericThrottlePipelineJsonAsync(
            string pipelineName,
            string provider,
            string model,
            string operation,
            string policyConfigJson)
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
                          "config": {
                            "concurrency": {
                              "enabled": true,
                              "maxDegreeOfParallelism": 8,
                              "jitter": false,
                              "policies": [
                                {
                                  "name": "concurrency.throttle",
                                  "config": {{policyConfigJson}}
                                }
                              ]
                            }
                          },
                          "steps": [
                            {
                              "name": "step-01",
                              "stepKey": "hello-world",
                              "order": 1,
                              "dependsOn": [],
                              "config": {
                                "provider": "{{provider}}",
                                "model": "{{model}}",
                                "operation": "{{operation}}",
                                "delayMs": 25,
                                "concurrency": {
                                  "enabled": true
                                }
                              }
                            },
                            {
                              "name": "step-02",
                              "stepKey": "hello-world",
                              "order": 2,
                              "dependsOn": [],
                              "config": {
                                "provider": "{{provider}}",
                                "model": "{{model}}",
                                "operation": "{{operation}}",
                                "delayMs": 25,
                                "concurrency": {
                                  "enabled": true
                                }
                              }
                            }
                          ]
                        }
                      ]
                    }
                    """;

            await File.WriteAllTextAsync(fullPath, json);

            return relativePath;
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