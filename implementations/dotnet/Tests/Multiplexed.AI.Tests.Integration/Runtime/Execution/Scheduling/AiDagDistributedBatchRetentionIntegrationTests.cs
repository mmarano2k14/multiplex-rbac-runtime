using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Scheduling
{
    /// <summary>
    /// Integration tests validating distributed batch DAG execution with retention.
    /// </summary>
    public sealed class AiDagDistributedBatchRetentionIntegrationTests
    {
        private const int StepCount = 50;
        private const int MaxCompletedStepsInState = 10;
        private const int PayloadSize = 4096;

        private readonly ITestOutputHelper _output;

        public AiDagDistributedBatchRetentionIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecuteBatchAsync_Should_Complete_50_Step_Dag_With_Compaction_And_Eviction()
        {
            var pipeline = CreateDependentParallelPipeline();

            await using var host = await CreateHost(pipeline);

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

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
            var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();
            var payloadResolver = host.ServiceProvider.GetRequiredService<IAiPayloadStoreResolver>();
            var payloadStore = payloadResolver.Resolve();

            var finalState = await dagStore.GetStateAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(finalState);

            _output.WriteLine(
                $"ExecutionId='{created.ExecutionId}', HotSteps='{finalState!.Steps.Count}'.");

            Assert.True(
                finalState.Steps.Count <= MaxCompletedStepsInState,
                $"Hot state should be bounded. Actual={finalState.Steps.Count}, Max={MaxCompletedStepsInState}");

            var archivedEntries = await indexStore.GetByExecutionAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(archivedEntries);
            Assert.NotEmpty(archivedEntries);

            Assert.True(
                archivedEntries.Count >= StepCount - finalState.Steps.Count,
                $"Expected archived entries for evicted steps. Archived={archivedEntries.Count}, Hot={finalState.Steps.Count}");

            var archivedEntry = archivedEntries
                .OrderBy(x => x.ArchivedAtUtc)
                .FirstOrDefault(x => !finalState.Steps.ContainsKey(x.StepName));

            Assert.NotNull(archivedEntry);

            var resolver = host.ServiceProvider.GetRequiredService<IAiExecutionStepResolver>();

            var resolvedArchivedStep = await resolver.GetStepAsync(
                created.ExecutionId,
                archivedEntry!.StepName,
                finalState,
                CancellationToken.None);

            Assert.NotNull(resolvedArchivedStep);
            Assert.True(resolvedArchivedStep!.IsCompleted);
            Assert.NotNull(resolvedArchivedStep.Result);
            Assert.True(resolvedArchivedStep.Result.Success);

            var payload = resolvedArchivedStep.Result.DataPayloads?.Values
                .FirstOrDefault(x => !x.IsInline && !string.IsNullOrWhiteSpace(x.ArtifactId));

            Assert.NotNull(payload);

            var loadedPayload = await payloadStore.LoadAsync(
                payload!.ArtifactId!,
                CancellationToken.None);

            Assert.NotNull(loadedPayload);
        }

        private static async Task<AiDagExecutionEngineTestHost> CreateHost(
            AiPipelineDefinition pipeline)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",
                PayloadStore = new AiPayloadStoreOptions
                {
                    Enabled = true,
                    Provider = "mongo-redis",
                    RequireReplaySafePayloads = true,
                    MaxInlineSizeBytes = 512,

                    Mongo = new MongoAiPayloadStoreOptions
                    {
                        Enabled = true,
                        ConnectionString = "mongodb://localhost:27017",
                        DatabaseName = "multiplexed_ai_tests",
                        CollectionName = $"payloads_batch_retention_{Guid.NewGuid():N}"
                    },

                    RedisCache = new RedisAiPayloadCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:payload:batch-retention:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        MaxCacheablePayloadBytes = 1024 * 1024
                    },

                    StepIndexCache = new RedisAiStepPayloadIndexCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:step-index:batch-retention:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        RefreshTtlOnRead = true
                    }
                },

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

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    var provider = new InMemoryAiPipelineDefinitionProvider(new[] { pipeline });

                    services.RemoveAll<IAiPipelineDefinitionProvider>();
                    services.RemoveAll<InMemoryAiPipelineDefinitionProvider>();

                    services.AddSingleton<IAiPipelineDefinitionProvider>(provider);
                    services.AddSingleton(provider);

                    services.AddAiStepsFromAssemblies(
                        typeof(AiDagDistributedBatchRetentionIntegrationTests).Assembly);
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        private static AiPipelineDefinition CreateDependentParallelPipeline()
        {
            var steps = new List<AiPipelineStepDefinition>();

            for (var i = 1; i <= StepCount; i++)
            {
                steps.Add(new AiPipelineStepDefinition
                {
                    Name = $"step-{i:00}",
                    StepKey = "test.batch.retention",
                    Order = i,
                    DependsOn = GetDependencies(i),
                    Config = new Dictionary<string, object?>
                    {
                        ["size"] = PayloadSize
                    }
                });
            }

            return new AiPipelineDefinition
            {
                Name = $"batch-retention-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Config = CreateRetentionConfig(),
                Steps = steps
            };
        }

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

        private static Dictionary<string, object?> CreateRetentionConfig()
        {
            return new Dictionary<string, object?>
            {
                ["retention"] = new Dictionary<string, object?>
                {
                    ["enabled"] = true,
                    ["policies"] = new[]
                    {
                        "retention.compact.terminal",
                        "retention.evict.terminal"
                    },
                    ["archiveReason"] = "batch-parallel-retention-test",
                    ["trigger"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["maxStepsInState"] = MaxCompletedStepsInState,
                        ["maxCompletedStepsInState"] = MaxCompletedStepsInState,
                        ["maxInlinePayloadBytes"] = 1
                    }
                }
            };
        }

        [AiStep("test.batch.retention")]
        private sealed class BatchRetentionStep : IAiStep
        {
            public string Name => "test.batch.retention";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext ctx,
                CancellationToken ct = default)
            {
                var size = Convert.ToInt32(ctx.Step.Config["size"]);

                return Task.FromResult(new AiStepResult
                {
                    Success = true,
                    Data = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["content"] = new string('x', size),
                            ["step"] = ctx.Step.Name
                        }
                    }
                });
            }
        }
    }
}