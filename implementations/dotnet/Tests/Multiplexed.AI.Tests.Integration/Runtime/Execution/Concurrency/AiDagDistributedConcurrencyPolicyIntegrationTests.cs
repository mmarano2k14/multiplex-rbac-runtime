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
    /// Integration tests for policy-aware distributed DAG concurrency admission.
    /// </summary>
    /// <remarks>
    /// These tests validate the complete runtime path:
    /// JSON pipeline configuration, concurrency definition resolution, policy-engine creation,
    /// provider/model admission policy evaluation, distributed claim admission, and DAG state behavior.
    /// </remarks>
    public sealed class AiDagDistributedConcurrencyPolicyIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";

        /// <summary>
        /// Verifies that a provider admission policy can prevent a ready DAG step from being claimed
        /// and executed.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Not_Execute_Step_When_ProviderAdmissionPolicy_Denies()
        {
            var pipelineName = $"dag-provider-concurrency-policy-deny-{Guid.NewGuid():N}";
            var jsonPath = await CreateProviderPolicyPipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-4.1",
                policyConfigJson: """
                {
                  "blockedProviders": [ "openai" ],
                  "reason": "OpenAI is blocked for this test.",
                  "retryAfterMs": 100
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

                Assert.NotEqual(AiExecutionStatus.Completed, record.Status);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var state = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(state);
                Assert.True(state!.Steps.ContainsKey("step-01"));

                var step = state.Steps["step-01"];

                // Policy denial happens before Redis claim and before step execution.
                // Therefore the step remains in its initial persisted state.
                Assert.Equal(AiStepExecutionStatus.None, step.Status);
                Assert.False(step.IsCompleted);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that a provider admission policy allows execution when the provider is explicitly allowed.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Execute_Step_When_ProviderAdmissionPolicy_Allows()
        {
            var pipelineName = $"dag-provider-concurrency-policy-allow-{Guid.NewGuid():N}";
            var jsonPath = await CreateProviderPolicyPipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-4.1",
                policyConfigJson: """
                {
                  "allowedProviders": [ "openai", "anthropic" ],
                  "requireProvider": true,
                  "retryAfterMs": 100
                }
                """);

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

                var state = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(state);
                Assert.True(state!.Steps.ContainsKey("step-01"));

                var step = state.Steps["step-01"];

                Assert.True(step.IsCompleted);
                Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that a model admission policy can prevent a ready DAG step from being claimed
        /// and executed when the provider/model pair is blocked.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Not_Execute_Step_When_ModelAdmissionPolicy_Denies()
        {
            var pipelineName = $"dag-model-concurrency-policy-deny-{Guid.NewGuid():N}";
            var jsonPath = await CreateModelPolicyPipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-3.5",
                policyConfigJson: """
                {
                  "blockedModels": [ "openai:gpt-3.5" ],
                  "reason": "Model is blocked for this test.",
                  "retryAfterMs": 100
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

                Assert.NotEqual(AiExecutionStatus.Completed, record.Status);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var state = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(state);
                Assert.True(state!.Steps.ContainsKey("step-01"));

                var step = state.Steps["step-01"];

                // Policy denial happens before Redis claim and before step execution.
                // Therefore the step remains in its initial persisted state.
                Assert.Equal(AiStepExecutionStatus.None, step.Status);
                Assert.False(step.IsCompleted);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that a model admission policy allows execution when the provider/model pair
        /// is explicitly allowed.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Execute_Step_When_ModelAdmissionPolicy_Allows()
        {
            var pipelineName = $"dag-model-concurrency-policy-allow-{Guid.NewGuid():N}";
            var jsonPath = await CreateModelPolicyPipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-4.1",
                policyConfigJson: """
                {
                  "allowedModels": [ "openai:gpt-4.1", "openai:gpt-4o" ],
                  "requireModel": true,
                  "requireProvider": true,
                  "retryAfterMs": 100
                }
                """);

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

                var state = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(state);
                Assert.True(state!.Steps.ContainsKey("step-01"));

                var step = state.Steps["step-01"];

                Assert.True(step.IsCompleted);
                Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that an operation admission policy can prevent a ready DAG step from being claimed
        /// and executed when the logical operation is blocked.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Not_Execute_Step_When_OperationAdmissionPolicy_Denies()
        {
            var pipelineName = $"dag-operation-concurrency-policy-deny-{Guid.NewGuid():N}";
            var jsonPath = await CreateOperationPolicyPipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-4.1",
                operation: "tool.dangerous",
                policyConfigJson: """
        {
          "blockedOperations": [ "tool.dangerous" ],
          "reason": "Operation is blocked for this test.",
          "retryAfterMs": 100
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

                Assert.NotEqual(AiExecutionStatus.Completed, record.Status);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var state = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(state);
                Assert.True(state!.Steps.ContainsKey("step-01"));

                var step = state.Steps["step-01"];

                // Policy denial happens before Redis claim and before step execution.
                // Therefore the step remains in its initial persisted state.
                Assert.Equal(AiStepExecutionStatus.None, step.Status);
                Assert.False(step.IsCompleted);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that an operation admission policy allows execution when the operation
        /// is explicitly allowed.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Execute_Step_When_OperationAdmissionPolicy_Allows()
        {
            var pipelineName = $"dag-operation-concurrency-policy-allow-{Guid.NewGuid():N}";
            var jsonPath = await CreateOperationPolicyPipelineJsonAsync(
                pipelineName,
                provider: "openai",
                model: "gpt-4.1",
                operation: "llm.chat",
                policyConfigJson: """
        {
          "allowedOperations": [ "llm.chat", "rag.retrieve" ],
          "requireOperation": true,
          "retryAfterMs": 100
        }
        """);

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

                var state = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(state);
                Assert.True(state!.Steps.ContainsKey("step-01"));

                var step = state.Steps["step-01"];

                Assert.True(step.IsCompleted);
                Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that a pipeline-level generic provider throttle limits concurrent DAG admission
        /// for matching provider steps.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        /// <remarks>
        /// This test validates the full runtime path for generic throttling:
        /// pipeline config policy, throttle rule resolution, context-target matching,
        /// effective concurrency definition creation, Redis provider-scope admission,
        /// and release after execution.
        /// </remarks>
        [Fact]
        public async Task ExecuteBatchAsync_Should_Throttle_OpenAi_Provider_From_Pipeline_Generic_Throttle_Policy()
        {
            var pipelineName = $"dag-generic-provider-throttle-{Guid.NewGuid():N}";
            var jsonPath = await CreateProviderThrottlePipelineJsonAsync(
                pipelineName,
                provider: "openai",
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

                Assert.All(
                    finalState!.Steps.Values,
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
        /// Creates test runtime options using the JSON pipeline definition source.
        /// </summary>
        /// <param name="jsonPipelineDefinitionFilePath">
        /// The relative JSON pipeline definition file path.
        /// </param>
        /// <returns>
        /// The runtime engine options.
        /// </returns>
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
        /// Creates a temporary JSON pipeline definition with a provider admission concurrency policy.
        /// </summary>
        /// <param name="pipelineName">
        /// The generated pipeline name.
        /// </param>
        /// <param name="provider">
        /// The provider value written into the step config.
        /// </param>
        /// <param name="model">
        /// The model value written into the step config.
        /// </param>
        /// <param name="policyConfigJson">
        /// The JSON object used as the configured provider admission policy config.
        /// </param>
        /// <returns>
        /// The relative path to the generated JSON pipeline definition.
        /// </returns>
        private static async Task<string> CreateProviderPolicyPipelineJsonAsync(
            string pipelineName,
            string provider,
            string model,
            string policyConfigJson)
        {
            return await CreatePolicyPipelineJsonAsync(
                    pipelineName,
                    provider,
                    model,
                    policyName: "concurrency.provider.admission",
                    policyConfigJson)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a temporary JSON pipeline definition with a model admission concurrency policy.
        /// </summary>
        /// <param name="pipelineName">
        /// The generated pipeline name.
        /// </param>
        /// <param name="provider">
        /// The provider value written into the step config.
        /// </param>
        /// <param name="model">
        /// The model value written into the step config.
        /// </param>
        /// <param name="policyConfigJson">
        /// The JSON object used as the configured model admission policy config.
        /// </param>
        /// <returns>
        /// The relative path to the generated JSON pipeline definition.
        /// </returns>
        private static async Task<string> CreateModelPolicyPipelineJsonAsync(
            string pipelineName,
            string provider,
            string model,
            string policyConfigJson)
        {
            return await CreatePolicyPipelineJsonAsync(
                    pipelineName,
                    provider,
                    model,
                    policyName: "concurrency.model.admission",
                    policyConfigJson)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a temporary JSON pipeline definition with a configured concurrency admission policy.
        /// </summary>
        /// <param name="pipelineName">
        /// The generated pipeline name.
        /// </param>
        /// <param name="provider">
        /// The provider value written into the step config.
        /// </param>
        /// <param name="model">
        /// The model value written into the step config.
        /// </param>
        /// <param name="policyName">
        /// The configured concurrency policy name.
        /// </param>
        /// <param name="policyConfigJson">
        /// The JSON object used as the configured policy config.
        /// </param>
        /// <returns>
        /// The relative path to the generated JSON pipeline definition.
        /// </returns>
        private static async Task<string> CreatePolicyPipelineJsonAsync(
            string pipelineName,
            string provider,
            string model,
            string policyName,
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
                              "maxDegreeOfParallelism": 4,
                              "jitter": false
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
                                "operation": "llm.chat",
                                "delayMs": 10,
                                "concurrency": {
                                  "enabled": true,
                                  "policies": [
                                    {
                                      "name": "{{policyName}}",
                                      "config": {{policyConfigJson}}
                                    }
                                  ]
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
        /// Creates a temporary JSON pipeline definition with an operation admission concurrency policy.
        /// </summary>
        /// <param name="pipelineName">
        /// The generated pipeline name.
        /// </param>
        /// <param name="provider">
        /// The provider value written into the step config.
        /// </param>
        /// <param name="model">
        /// The model value written into the step config.
        /// </param>
        /// <param name="operation">
        /// The operation value written into the step config.
        /// </param>
        /// <param name="policyConfigJson">
        /// The JSON object used as the configured operation admission policy config.
        /// </param>
        /// <returns>
        /// The relative path to the generated JSON pipeline definition.
        /// </returns>
        private static async Task<string> CreateOperationPolicyPipelineJsonAsync(
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
                      "maxDegreeOfParallelism": 4,
                      "jitter": false
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
                        "delayMs": 10,
                        "concurrency": {
                          "enabled": true,
                          "policies": [
                            {
                              "name": "concurrency.operation.admission",
                              "config": {{policyConfigJson}}
                            }
                          ]
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
        /// Creates a temporary JSON pipeline definition with a pipeline-level generic provider throttle policy.
        /// </summary>
        /// <param name="pipelineName">
        /// The generated pipeline name.
        /// </param>
        /// <param name="provider">
        /// The provider value written into each step config.
        /// </param>
        /// <param name="policyConfigJson">
        /// The JSON object used as the configured generic throttle policy config.
        /// </param>
        /// <returns>
        /// The relative path to the generated JSON pipeline definition.
        /// </returns>
        private static async Task<string> CreateProviderThrottlePipelineJsonAsync(
            string pipelineName,
            string provider,
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
                        "model": "gpt-4.1",
                        "operation": "llm.chat",
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
                        "model": "gpt-4.1",
                        "operation": "llm.chat",
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
        /// Deletes the temporary JSON pipeline definition file.
        /// </summary>
        /// <param name="relativePath">
        /// The relative JSON pipeline definition path.
        /// </param>
        private static void DeletePipelineJson(
            string relativePath)
        {
            var fullPath = Path.Combine(
                AppContext.BaseDirectory,
                relativePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}