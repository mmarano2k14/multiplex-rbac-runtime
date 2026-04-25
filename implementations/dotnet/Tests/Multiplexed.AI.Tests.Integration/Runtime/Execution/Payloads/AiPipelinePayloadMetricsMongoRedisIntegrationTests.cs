using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Payloads
{
    public sealed class AiPipelinePayloadMetricsMongoRedisIntegrationTests
    {
        [Fact]
        public async Task CompactorLevel_DynamicStepResults_Should_Record_PayloadMetrics_And_Use_MongoRedis_Cache()
        {
            const int stepCount = 20;
            const int inlineThresholdBytes = 1024;

            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            var redisDb = redis.GetDatabase();

            var metrics = new InMemoryAiPayloadMetrics();

            var options = Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = "mongo-redis",
                RequireReplaySafePayloads = true,
                Mongo = new MongoAiPayloadStoreOptions
                {
                    Enabled = true,
                    ConnectionString = "mongodb://localhost:27017",
                    DatabaseName = "multiplexed_ai_tests",
                    CollectionName = $"payloads_{Guid.NewGuid():N}"
                },
                RedisCache = new RedisAiPayloadCacheOptions
                {
                    Enabled = true,
                    KeyPrefix = $"test:ai:payload:{Guid.NewGuid():N}",
                    ExpirationSeconds = 60,
                    MaxCacheablePayloadBytes = 1024 * 1024
                },
                MaxInlineSizeBytes = inlineThresholdBytes
            });

            var mongoStore = new MongoAiPayloadStore(options);

            var payloadStore = new MongoRedisCachedAiPayloadStore(
                mongoStore,
                redis,
                options,
                metrics);

            var resolver = new TestPayloadStoreResolver(payloadStore);

            var dataPolicy = new SmartInlineAiExecutionDataPolicy(
                resolver,
                options);

            var compactor = new DefaultAiStepResultPayloadCompactor(
                dataPolicy,
                metrics);

            var results = CreateDynamicStepResults(stepCount);

            foreach (var result in results)
            {
                await compactor.CompactAsync(result);
            }

            var snapshot = metrics.Snapshot();

            Assert.Equal(stepCount / 2, snapshot.InlineCount);
            Assert.Equal(stepCount / 2, snapshot.ExternalizedCount);
            Assert.True(snapshot.ExternalizedBytes > snapshot.InlineBytes);
        }

        [Fact]
        public async Task EngineLevel_CodeFirstPipeline_Should_Record_PayloadMetrics_And_Use_MongoRedis_Cache()
        {
            const int stepCount = 20;

            var pipelineName = $"payload-metrics-{Guid.NewGuid():N}";

            var engineOptions = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",
                PayloadStore = new AiPayloadStoreOptions
                {
                    Enabled = true,
                    Provider = "mongo-redis",
                    RequireReplaySafePayloads = true,
                    MaxInlineSizeBytes = 1024,
                    Mongo = new MongoAiPayloadStoreOptions
                    {
                        Enabled = true,
                        ConnectionString = "mongodb://localhost:27017",
                        DatabaseName = "multiplexed_ai_tests",
                        CollectionName = $"payloads_{Guid.NewGuid():N}"
                    },
                    RedisCache = new RedisAiPayloadCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:payload:{Guid.NewGuid():N}",
                        ExpirationSeconds = 60,
                        MaxCacheablePayloadBytes = 1024 * 1024
                    }
                }
            };

            var definition = CreatePipeline(pipelineName, stepCount);

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                engineOptions,
                services =>
                {
                    // 🔥 FIX ICI
                    var provider = new InMemoryAiPipelineDefinitionProvider(new[] { definition });

                    services.RemoveAll<IAiPipelineDefinitionProvider>();
                    services.RemoveAll<InMemoryAiPipelineDefinitionProvider>();

                    services.AddSingleton<IAiPipelineDefinitionProvider>(provider);
                    services.AddSingleton(provider);

                    services.AddAiStepsFromAssemblies(
                        typeof(AiPipelinePayloadMetricsMongoRedisIntegrationTests).Assembly);
                },
                mongoConnectionString: "mongodb://localhost:27017",
                mongoDatabaseName: "multiplexed_ai_tests",
                redisConnectionString: "localhost:6379");

            var engine = host.Engine;
            var metrics = host.ServiceProvider.GetRequiredService<IAiPayloadMetrics>();

            var created = await engine.CreateAsync(pipelineName, new Dictionary<string, object?>());
            var result = await engine.ExecuteAllAsync(created.ExecutionId);

            Assert.Equal(AiExecutionStatus.Completed, result.Status);
            Assert.Equal(stepCount, result.CompletedSteps.Count);

            var snapshot = ((InMemoryAiPayloadMetrics)metrics).Snapshot();

            Assert.Equal(stepCount / 2, snapshot.InlineCount);
            Assert.Equal(stepCount / 2, snapshot.ExternalizedCount);
        }

        [Fact]
        public async Task EngineLevel_LongRun_CodeFirstPipeline_Should_Record_Stable_PayloadMetrics_With_MongoRedis_Cache()
        {
            const int stepCount = 200;
            const int inlineThresholdBytes = 1024;

            var pipelineName = $"payload-longrun-{Guid.NewGuid():N}";

            var engineOptions = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",
                PayloadStore = new AiPayloadStoreOptions
                {
                    Enabled = true,
                    Provider = "mongo-redis",
                    RequireReplaySafePayloads = true,
                    MaxInlineSizeBytes = inlineThresholdBytes,
                    Mongo = new MongoAiPayloadStoreOptions
                    {
                        Enabled = true,
                        ConnectionString = "mongodb://localhost:27017",
                        DatabaseName = "multiplexed_ai_tests",
                        CollectionName = $"payloads_longrun_{Guid.NewGuid():N}"
                    },
                    RedisCache = new RedisAiPayloadCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:payload:longrun:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        MaxCacheablePayloadBytes = 1024 * 1024
                    }
                }
            };

            var definition = CreatePipeline(pipelineName, stepCount);

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                engineOptions,
                services =>
                {
                    var provider = new InMemoryAiPipelineDefinitionProvider(new[] { definition });

                    services.RemoveAll<IAiPipelineDefinitionProvider>();
                    services.RemoveAll<InMemoryAiPipelineDefinitionProvider>();

                    services.AddSingleton<IAiPipelineDefinitionProvider>(provider);
                    services.AddSingleton(provider);

                    services.AddAiStepsFromAssemblies(
                        typeof(AiPipelinePayloadMetricsMongoRedisIntegrationTests).Assembly);
                },
                mongoConnectionString: "mongodb://localhost:27017",
                mongoDatabaseName: "multiplexed_ai_tests",
                redisConnectionString: "localhost:6379");

            var created = await host.Engine.CreateAsync(
                pipelineName,
                new Dictionary<string, object?>());

            var finalRecord = await host.Engine.ExecuteAllAsync(created.ExecutionId);

            Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
            Assert.Equal(stepCount, finalRecord.CompletedSteps.Count);

            var metrics = host.ServiceProvider.GetRequiredService<IAiPayloadMetrics>();
            var snapshot = ((InMemoryAiPayloadMetrics)metrics).Snapshot();

            Assert.Equal(stepCount / 2, snapshot.InlineCount);
            Assert.Equal(stepCount / 2, snapshot.ExternalizedCount);

            Assert.True(snapshot.InlineBytes > 0);
            Assert.True(snapshot.ExternalizedBytes > snapshot.InlineBytes);

            Assert.True(snapshot.CacheWriteCount >= stepCount / 2);

            Assert.Equal(0, snapshot.CacheFallbackCount);
            Assert.Equal(0, snapshot.CacheMissCount);
        }

        private static AiPipelineDefinition CreatePipeline(string name, int steps)
        {
            var list = new List<AiPipelineStepDefinition>();

            for (int i = 0; i < steps; i++)
            {
                list.Add(new AiPipelineStepDefinition
                {
                    Name = $"step-{i}",
                    StepKey = "test.payload",
                    Order = i,
                    DependsOn = i == 0 ? new List<string>() : new List<string> { $"step-{i - 1}" },
                    Config = new Dictionary<string, object?>
                    {
                        ["size"] = i % 2 == 0 ? 128 : 4096
                    }
                });
            }

            return new AiPipelineDefinition
            {
                Name = name,
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = list
            };
        }

        [AiStep("test.payload")]
        private sealed class TestStep : IAiStep
        {
            public string Name => "test.payload";

            public Task<AiStepResult> ExecuteAsync(AiStepExecutionContext context, CancellationToken ct = default)
            {
                var size = Convert.ToInt32(context.Step.Config["size"]);

                return Task.FromResult(new AiStepResult
                {
                    Success = true,
                    Data = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["content"] = new string('x', size)
                        }
                    }
                });
            }
        }

        private static IReadOnlyList<AiStepResult> CreateDynamicStepResults(int count)
        {
            var list = new List<AiStepResult>();

            for (int i = 0; i < count; i++)
            {
                list.Add(new AiStepResult
                {
                    Data = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["content"] = new string('x', i % 2 == 0 ? 128 : 4096)
                        }
                    }
                });
            }

            return list;
        }

        private sealed class TestPayloadStoreResolver : IAiPayloadStoreResolver
        {
            private readonly IAiPayloadStore _store;
            public TestPayloadStoreResolver(IAiPayloadStore store) => _store = store;
            public IAiPayloadStore Resolve() => _store;
        }
    }
}