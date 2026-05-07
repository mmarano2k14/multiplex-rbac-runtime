using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Retention
{
    /// <summary>
    /// Production-like integration tests for config-driven policy-based hybrid retention.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate Hybrid retention with the real DAG execution engine.
    /// - Validate the new policy-driven retention engine through runtime execution.
    /// - Validate archive index + step payload store integration through the fixture.
    /// - Validate resolver visibility after eviction.
    ///
    /// IMPORTANT:
    /// - These tests intentionally do not configure legacy StateRetention options.
    /// - These tests intentionally do not configure legacy RetentionTrigger options.
    /// - Retention is configured only through pipeline-level JSON configuration.
    /// - Terminal re-entry is validated through persisted state and archive visibility,
    ///   not by calling ExecuteAllAsync again on an already-terminal execution.
    /// </remarks>
    public sealed class AiDagPolicyDrivenHybridRetentionProductionIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";
        private const int MaxCompletedStepsInState = 3;
        private const int MaxInlinePayloadBytes = 1;

        /// <summary>
        /// Verifies that a real DAG execution can complete with config-driven Hybrid retention enabled,
        /// and that at least one archived step remains resolvable after eviction.
        /// </summary>
        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_With_Config_Driven_Hybrid_Retention_And_Archived_Steps_Resolvable()
        {
            var pipelineName = $"dag-policy-driven-hybrid-retention-{Guid.NewGuid():N}";
            var jsonPath = await CreatePipelineJsonAsync(pipelineName);

            try
            {
                var options = CreateOptions(jsonPath);

                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    options,
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(
                    pipelineName,
                    "hello");

                var finalRecord = await host.Engine
                    .ExecuteAllAsync(created.ExecutionId)
                    .WaitAsync(TimeSpan.FromSeconds(30));

                Assert.NotNull(finalRecord);
                Assert.True(
                    finalRecord.IsTerminal,
                    $"Execution should be terminal. Status={finalRecord.Status}");

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
                var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();
                var payloadStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadStore>();

                var finalState = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(finalState);
                Assert.NotEmpty(finalState!.Steps);

                Assert.True(
                    finalState.Steps.Count <= MaxCompletedStepsInState,
                    $"Hot state should be bounded. Actual={finalState.Steps.Count}, Max={MaxCompletedStepsInState}");

                var archivedEntries = await indexStore.GetByExecutionAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(archivedEntries);
                Assert.NotEmpty(archivedEntries);

                var archivedEntry = archivedEntries
                    .OrderBy(x => x.ArchivedAtUtc)
                    .FirstOrDefault(x => !finalState.Steps.ContainsKey(x.StepName));

                Assert.NotNull(archivedEntry);

                var resolver = new DefaultAiExecutionStepResolver(
                    indexStore,
                    payloadStore);

                var statusOnlyStep = await resolver.GetStepStatusAsync(
                    created.ExecutionId,
                    archivedEntry!.StepName,
                    finalState,
                    CancellationToken.None);

                Assert.NotNull(statusOnlyStep);
                Assert.Equal(archivedEntry.StepName, statusOnlyStep!.StepName);
                Assert.Equal(archivedEntry.Status, statusOnlyStep.Status);

                var fullArchivedStep = await resolver.GetStepAsync(
                    created.ExecutionId,
                    archivedEntry.StepName,
                    finalState,
                    CancellationToken.None);

                Assert.NotNull(fullArchivedStep);
                Assert.Equal(archivedEntry.StepName, fullArchivedStep!.StepName);
                Assert.Equal(archivedEntry.Status, fullArchivedStep.Status);
                Assert.True(fullArchivedStep.IsCompleted);
                Assert.NotNull(fullArchivedStep.Result);
                Assert.True(fullArchivedStep.Result.Success);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Verifies that persisted terminal retention state is stable and reloadable.
        /// </summary>
        /// <remarks>
        /// This intentionally does not call ExecuteAllAsync a second time on a terminal execution.
        /// Terminal idempotence is validated through persisted record, bounded state, archive index,
        /// and resolver visibility.
        /// </remarks>
        [Fact]
        public async Task Terminal_Retention_State_Should_Remain_Stable_And_Archived_Steps_Resolvable()
        {
            var pipelineName = $"dag-policy-driven-hybrid-retention-idempotent-{Guid.NewGuid():N}";
            var jsonPath = await CreatePipelineJsonAsync(pipelineName);

            try
            {
                var options = CreateOptions(jsonPath);

                await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                    options,
                    mongoConnectionString: ConnectionString,
                    mongoDatabaseName: DatabaseName);

                var created = await host.Engine.CreateAsync(
                    pipelineName,
                    "hello");

                var first = await host.Engine
                    .ExecuteAllAsync(created.ExecutionId)
                    .WaitAsync(TimeSpan.FromSeconds(30));

                Assert.True(first.IsTerminal);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
                var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();
                var payloadStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadStore>();

                var reloadedRecord = await dagStore.GetRecordAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(reloadedRecord);
                Assert.True(reloadedRecord!.IsTerminal);
                Assert.Equal(first.ExecutionId, reloadedRecord.ExecutionId);
                Assert.Equal(first.Status, reloadedRecord.Status);

                var reloadedState = await dagStore.GetStateAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(reloadedState);
                Assert.NotEmpty(reloadedState!.Steps);

                Assert.True(
                    reloadedState.Steps.Count <= MaxCompletedStepsInState,
                    $"Hot state should remain bounded. Actual={reloadedState.Steps.Count}, Max={MaxCompletedStepsInState}");

                var archivedEntries = await indexStore.GetByExecutionAsync(
                    created.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(archivedEntries);
                Assert.NotEmpty(archivedEntries);

                var archivedEntry = archivedEntries
                    .OrderBy(x => x.ArchivedAtUtc)
                    .FirstOrDefault(x => !reloadedState.Steps.ContainsKey(x.StepName));

                Assert.NotNull(archivedEntry);

                var resolver = new DefaultAiExecutionStepResolver(
                    indexStore,
                    payloadStore);

                var resolved = await resolver.GetStepAsync(
                    created.ExecutionId,
                    archivedEntry!.StepName,
                    reloadedState,
                    CancellationToken.None);

                Assert.NotNull(resolved);
                Assert.True(resolved!.IsCompleted);
                Assert.NotNull(resolved.Result);
                Assert.True(resolved.Result.Success);
            }
            finally
            {
                DeletePipelineJson(jsonPath);
            }
        }

        /// <summary>
        /// Creates production-like test options for fast policy-driven Hybrid retention validation.
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
        /// Creates a temporary JSON DAG pipeline definition with pipeline-level retention config.
        /// </summary>
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
                  "config": {
                    "retention": {
                      "enabled": true,
                      "policies": [
                        "retention.compact.terminal",
                        "retention.evict.terminal"
                      ],
                      "archiveReason": "policy-driven-hybrid-production-test",
                      "trigger": {
                        "enabled": true,
                        "maxStepsInState": {{MaxCompletedStepsInState}},
                        "maxCompletedStepsInState": {{MaxCompletedStepsInState}},
                        "maxInlinePayloadBytes": {{MaxInlinePayloadBytes}}
                      }
                    }
                  },
                  "steps": [
                    {
                      "name": "step-1",
                      "stepKey": "hello-world",
                      "order": 1,
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-2",
                      "stepKey": "hello-world",
                      "order": 2,
                      "dependsOn": [ "step-1" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-3",
                      "stepKey": "hello-world",
                      "order": 3,
                      "dependsOn": [ "step-1" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-4",
                      "stepKey": "hello-world",
                      "order": 4,
                      "dependsOn": [ "step-2" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-5",
                      "stepKey": "hello-world",
                      "order": 5,
                      "dependsOn": [ "step-2" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-6",
                      "stepKey": "hello-world",
                      "order": 6,
                      "dependsOn": [ "step-3" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-7",
                      "stepKey": "hello-world",
                      "order": 7,
                      "dependsOn": [ "step-3" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-8",
                      "stepKey": "hello-world",
                      "order": 8,
                      "dependsOn": [ "step-4", "step-5" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-9",
                      "stepKey": "hello-world",
                      "order": 9,
                      "dependsOn": [ "step-6", "step-7" ],
                      "config": { "delayMs": 10 }
                    },
                    {
                      "name": "step-10",
                      "stepKey": "hello-world",
                      "order": 10,
                      "dependsOn": [ "step-8", "step-9" ],
                      "config": { "delayMs": 10 }
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
            var fullPath = Path.Combine(AppContext.BaseDirectory, jsonPipelineDefinitionFilePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
