using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;
using Multiplexed.AI.Runtime.AI.Rag.Composition.Deterministic;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Runtime.AI.Rag.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Retrieval.MultiProvider;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    public sealed class FinalRagDagIntegrationTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Run_Compact_Rag_Pipeline_EndToEnd()
        {
            await using var host = await CreateRagHostAsync("config\\rag-compact.json");

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-compact",
                new Dictionary<string, object?>
                {
                    ["query"] = "Hello Rag",
                });

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            var payloadResolver =
                host.ServiceProvider.GetRequiredService<IAiExecutionPayloadResolver>();

            var multiStep = state!.Steps["retrieve"];
            Assert.True(multiStep.IsCompleted);

            var retrievalBatch = await GetResultDataValueAsync<RagRetrievalBatch>(
                multiStep.Result!,
                "batch",
                payloadResolver);

            Assert.NotNull(retrievalBatch);
            Assert.NotNull(retrievalBatch.Items);

            var composeStep = state.Steps["compose"];
            Assert.True(composeStep.IsCompleted);

            var structuredContext = await GetResultDataValueAsync<RagStructuredContext>(
                composeStep.Result!,
                "context",
                payloadResolver);

            Assert.NotNull(structuredContext);
            Assert.NotNull(structuredContext.OrderedTexts);
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Run_Expert_Rag_Pipeline_EndToEnd()
        {
            await using var host = await CreateRagHostAsync("config\\rag-expert.json");

            var engine = host.Engine;

            var created = await engine.CreateAsync("rag-expert",
                new Dictionary<string, object?>
                {
                    ["query"] = "Hello Rag Expert",
                });

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            var payloadResolver =
                host.ServiceProvider.GetRequiredService<IAiExecutionPayloadResolver>();

            var mergeStep = state!.Steps["merge"];

            var mergedBatch = await GetResultDataValueAsync<RagRetrievalBatch>(
                mergeStep.Result!,
                "batch",
                payloadResolver);

            Assert.NotNull(mergedBatch);
            Assert.NotNull(mergedBatch.Items);

            var composeStep = state.Steps["compose"];

            var context = await GetResultDataValueAsync<RagStructuredContext>(
                composeStep.Result!,
                "context",
                payloadResolver);

            var fragments = await GetResultDataValueAsync<IReadOnlyList<RagContextFragment>>(
                composeStep.Result!,
                "fragments",
                payloadResolver);

            Assert.NotNull(context);
            Assert.NotNull(fragments);

            for (var i = 0; i < fragments.Count; i++)
            {
                Assert.Equal(i, fragments[i].Order);
            }
        }

        // =============================
        // PAYLOAD-AWARE HELPERS
        // =============================

        private static async Task<T?> GetResultDataValueAsync<T>(
            AiStepResult result,
            string key,
            IAiExecutionPayloadResolver resolver)
        {
            if (result.DataPayloads != null &&
                result.DataPayloads.TryGetValue(key, out var payload))
            {
                var resolved = await resolver.ResolveAsync(payload);
                return ConvertValue<T>(resolved);
            }

            if (result.Data != null &&
                result.Data.TryGetValue(key, out var value))
            {
                return ConvertValue<T>(value);
            }

            return default;
        }

        private static T? ConvertValue<T>(object? raw)
        {
            if (raw == null)
                return default;

            if (raw is T t)
                return t;

            if (raw is JsonElement json)
                return json.Deserialize<T>();

            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(raw));
        }

        // =============================
        // EXISTING CODE UNCHANGED BELOW
        // =============================

        private static async Task<AiDagExecutionEngineTestHost> CreateRagHostAsync(string pipelinePath)
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = pipelinePath
            };

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddRagCore();
                    services.AddRagFromAssemblies(typeof(FinalRagDagIntegrationTests).Assembly);
                    RegisterRagTestServices(services);
                });
        }

        private static void RegisterRagTestServices(IServiceCollection services)
        {
            services.AddTransient<TestRedisVectorProvider>();
            services.AddTransient<TestHrSqlProvider>();
            services.AddTransient<TestRuntimeStateProvider>();
            services.AddTransient<MultiProviderRetrieval>();
            services.AddTransient<DeterministicComposer>();

            services.Replace(ServiceDescriptor.Singleton<IRagProviderRegistry>(
                _ => new DefaultRagProviderRegistry(new[]
                {
                    new RagProviderDescriptor { Key = "redis-vector", ImplementationType = typeof(TestRedisVectorProvider) },
                    new RagProviderDescriptor { Key = "hr-sql", ImplementationType = typeof(TestHrSqlProvider) },
                    new RagProviderDescriptor { Key = "runtime-state", ImplementationType = typeof(TestRuntimeStateProvider) }
                })));

            services.Replace(ServiceDescriptor.Singleton<IRagRetrievalRegistry>(
                _ => new DefaultRagRetrievalRegistry(new[]
                {
                    new RagRetrievalDescriptor { Key = "multi-provider", ImplementationType = typeof(MultiProviderRetrieval) }
                })));

            services.Replace(ServiceDescriptor.Singleton<IRagComposerRegistry>(
                _ => new DefaultRagComposerRegistry(new[]
                {
                    new RagComposerDescriptor { Key = "deterministic", ImplementationType = typeof(DeterministicComposer) }
                })));
        }

        private sealed class TestRedisVectorProvider : INormalizingRagProvider
        {
            public string Key => "redis-vector";

            public Task<RagRetrievalBatch> RetrieveNormalizedAsync(RagExecutionContext context, CancellationToken cancellationToken = default)
                => Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "vector-1",
                            ProviderKey = Key,
                            ProviderKind = RagProviderKind.Vector,
                            ContentText = $"Vector match for '{context.QueryText}'",
                            Score = 0.95
                        }
                    }
                });
        }

        private sealed class TestHrSqlProvider : INormalizingRagProvider
        {
            public string Key => "hr-sql";

            public Task<RagRetrievalBatch> RetrieveNormalizedAsync(RagExecutionContext context, CancellationToken cancellationToken = default)
                => Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "sql-1",
                            ProviderKey = Key,
                            ProviderKind = RagProviderKind.Structured,
                            ContentText = $"SQL row for '{context.QueryText}'",
                            Score = 0.80
                        }
                    }
                });
        }

        private sealed class TestRuntimeStateProvider : INormalizingRagProvider
        {
            public string Key => "runtime-state";

            public Task<RagRetrievalBatch> RetrieveNormalizedAsync(RagExecutionContext context, CancellationToken cancellationToken = default)
                => Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "runtime-1",
                            ProviderKey = Key,
                            ProviderKind = RagProviderKind.Runtime,
                            ContentText = $"Runtime state for '{context.QueryText}'",
                            Score = 0.50
                        }
                    }
                });
        }
    }
}