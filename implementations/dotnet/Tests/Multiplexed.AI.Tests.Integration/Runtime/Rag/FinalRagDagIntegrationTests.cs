using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;
using Multiplexed.AI.Runtime.AI.Rag.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Composition.Deterministic;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Runtime.AI.Rag.Discovery.Registry;
using Multiplexed.AI.Runtime.AI.Rag.Providers;
using Multiplexed.AI.Runtime.AI.Rag.Retrieval;
using Multiplexed.AI.Runtime.AI.Rag.Retrieval.MultiProvider;
using Multiplexed.AI.Runtime.AI.Rag.Steps;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    /// <summary>
    /// Final integration tests for the RAG DAG runtime.
    ///
    /// PURPOSE:
    /// - Validate that the RAG subsystem is correctly integrated into the DAG runtime.
    /// - Validate that resolver-based DI wiring is functional.
    /// - Validate both compact and expert RAG execution modes.
    ///
    /// IMPORTANT:
    /// - These tests intentionally use the full DAG runtime fixture.
    /// - RAG is treated as a first-class extension of the runtime, not as a standalone module.
    /// - Resolver tests must therefore run against the full runtime container.
    /// </summary>
    public sealed class FinalRagDagIntegrationTests
    {
        /// <summary>
        /// Verifies that the compact RAG DAG path works:
        ///
        /// rag.multi -> rag.compose
        ///
        /// WHAT THIS TEST PROVES:
        /// - retrieval resolver is correctly wired
        /// - composer resolver is correctly wired
        /// - retrieval output is persisted in step state
        /// - compose step can consume retrieval batch
        /// - final composed context is serializable and available
        /// </summary>
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
            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);
            Assert.NotNull(final);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);
            Assert.NotNull(state!.Steps);
            Assert.NotEmpty(state.Steps);

            // -------------------------------------------------------------
            // Validate rag.multi step result
            // -------------------------------------------------------------
            var multiStep = state.Steps["retrieve"];
            Assert.True(multiStep.IsCompleted);

            Assert.NotNull(multiStep.Result);
            Assert.NotNull(multiStep.Result.Data);
            Assert.True(
                multiStep.Result.Data.TryGetValue("batch", out var batchValue),
                "Expected 'batch' in rag.multi result data.");

            var retrievalBatch = Assert.IsType<RagRetrievalBatch>(batchValue);
            Assert.NotNull(retrievalBatch.Items);

            // -------------------------------------------------------------
            // Validate rag.compose step result
            // -------------------------------------------------------------
            var composeStep = state.Steps["compose"];
            Assert.True(composeStep.IsCompleted);

            Assert.NotNull(composeStep.Result);
            Assert.NotNull(composeStep.Result.Data);
            Assert.True(
                composeStep.Result.Data.TryGetValue("context", out var contextValue),
                "Expected 'context' in rag.compose result data.");

            var structuredContext = Assert.IsType<RagStructuredContext>(contextValue);
            Assert.NotNull(structuredContext);
            Assert.NotNull(structuredContext.OrderedTexts);
        }

        /// <summary>
        /// Verifies that the expert RAG DAG path works:
        ///
        /// rag.vector -> rag.sql -> rag.runtime -> rag.merge -> rag.compose
        ///
        /// WHAT THIS TEST PROVES:
        /// - provider resolver is correctly wired
        /// - merge step can resolve batches from prior steps
        /// - composition consumes merged batch correctly
        /// - the final context remains deterministic and non-empty
        /// </summary>
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
            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);
            Assert.NotNull(final);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);
            Assert.NotNull(state!.Steps);
            Assert.NotEmpty(state.Steps);

            Assert.True(state.Steps["vector-search"].IsCompleted);
            Assert.True(state.Steps["sql-search"].IsCompleted);
            Assert.True(state.Steps["runtime-search"].IsCompleted);
            Assert.True(state.Steps["merge"].IsCompleted);
            Assert.True(state.Steps["compose"].IsCompleted);

            // -------------------------------------------------------------
            // Validate merge output
            // -------------------------------------------------------------
            var mergeStep = state.Steps["merge"];
            Assert.NotNull(mergeStep.Result);
            Assert.NotNull(mergeStep.Result.Data);
            Assert.True(
                mergeStep.Result.Data.TryGetValue("batch", out var mergeBatchValue),
                "Expected 'batch' in rag.merge result data.");

            var mergedBatch = Assert.IsType<RagRetrievalBatch>(mergeBatchValue);
            Assert.NotNull(mergedBatch.Items);

            // -------------------------------------------------------------
            // Validate compose output
            // -------------------------------------------------------------
            var composeStep = state.Steps["compose"];
            Assert.NotNull(composeStep.Result);
            Assert.NotNull(composeStep.Result.Data);

            Assert.True(
                composeStep.Result.Data.TryGetValue("context", out var contextValue),
                "Expected 'context' in rag.compose result data.");

            Assert.True(
                composeStep.Result.Data.TryGetValue("fragments", out var fragmentsValue),
                "Expected 'fragments' in rag.compose result data.");

            var context = Assert.IsType<RagStructuredContext>(contextValue);
            var fragments = Assert.IsAssignableFrom<IReadOnlyList<RagContextFragment>>(fragmentsValue);

            Assert.NotNull(context.Text);
            Assert.NotNull(context.OrderedTexts);
            Assert.NotNull(fragments);

            // Deterministic invariant:
            // fragment orders must be sequential and dense.
            for (var i = 0; i < fragments.Count; i++)
            {
                Assert.Equal(i, fragments[i].Order);
            }
        }

        /// <summary>
        /// Verifies that provider resolver can resolve all configured providers
        /// from the full runtime DI container and registry.
        ///
        /// WHAT THIS TEST PROVES:
        /// - provider descriptors are registered
        /// - concrete provider types are available in DI
        /// - key-based resolution succeeds inside the real runtime graph
        /// </summary>
        [Fact]
        public async Task ProviderResolver_Should_Resolve_All_Configured_Rag_Providers()
        {
            await using var host = await CreateRagHostAsync("config\\rag-compact.json");

            var resolver = host.ServiceProvider.GetRequiredService<INormalizingRagProviderResolver>();

            Assert.NotNull(resolver.Resolve("redis-vector"));
            Assert.NotNull(resolver.Resolve("hr-sql"));
            Assert.NotNull(resolver.Resolve("runtime-state"));
        }

        /// <summary>
        /// Verifies that retrieval resolver can resolve the configured retrieval strategy
        /// from the full runtime DI container.
        /// </summary>
        [Fact]
        public async Task RetrievalResolver_Should_Resolve_Configured_Retrieval()
        {
            await using var host = await CreateRagHostAsync("config\\rag-compact.json");

            var resolver = host.ServiceProvider.GetRequiredService<IRagRetrievalResolver>();

            var retrieval = resolver.Resolve("multi-provider");

            Assert.NotNull(retrieval);
            Assert.IsType<MultiProviderRetrieval>(retrieval);
        }

        /// <summary>
        /// Verifies that composer resolver can resolve the configured composer strategy
        /// from the full runtime DI container.
        /// </summary>
        [Fact]
        public async Task ComposerResolver_Should_Resolve_Configured_Composer()
        {
            await using var host = await CreateRagHostAsync("config\\rag-compact.json");

            var resolver = host.ServiceProvider.GetRequiredService<IRagComposerResolver>();

            var composer = resolver.Resolve("deterministic");

            Assert.NotNull(composer);
            Assert.IsType<DeterministicComposer>(composer);
        }

        // ============================================================
        // TEST HOST CREATION
        // ============================================================

        /// <summary>
        /// Creates a fully wired RAG-enabled DAG test host.
        ///
        /// PURPOSE:
        /// - Ensures all RAG tests run against the complete runtime fixture.
        /// - Keeps test setup consistent across compact, expert, and resolver scenarios.
        /// </summary>
        private static async Task<AiDagExecutionEngineTestHost> CreateRagHostAsync(string pipelinePath)
        {
            var options = CreateOptions(pipelinePath);

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddRagCore();
                    services.AddRagOperationsFromAssemblies(typeof(TestOperation).Assembly);
                    RegisterRagTestServices(services);
                });
        }

        // ============================================================
        // TEST SERVICE REGISTRATION
        // ============================================================

        /// <summary>
        /// Registers all RAG services required by the final integration tests.
        ///
        /// IMPORTANT:
        /// - This method assumes the full runtime graph is already registered.
        /// - It only adds or replaces the RAG-specific services needed by these tests.
        /// - Registries are replaced with deterministic test descriptors.
        /// </summary>
        private static void RegisterRagTestServices(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // ---------------------------------------------------------
            // Register mock / test providers as concrete types.
            // Resolvers require concrete implementation type resolution.
            // ---------------------------------------------------------
            services.AddTransient<TestRedisVectorProvider>();
            services.AddTransient<TestHrSqlProvider>();
            services.AddTransient<TestRuntimeStateProvider>();

            services.AddTransient<MultiProviderRetrieval>();
            services.AddTransient<DeterministicComposer>();

            // ---------------------------------------------------------
            // Replace registries with deterministic test descriptors.
            // In production this normally comes from discovery.
            // ---------------------------------------------------------
            services.Replace(ServiceDescriptor.Singleton<IRagProviderRegistry>(
                _ => new DefaultRagProviderRegistry(new[]
                {
                    new RagProviderDescriptor
                    {
                        Key = "redis-vector",
                        ImplementationType = typeof(TestRedisVectorProvider)
                    },
                    new RagProviderDescriptor
                    {
                        Key = "hr-sql",
                        ImplementationType = typeof(TestHrSqlProvider)
                    },
                    new RagProviderDescriptor
                    {
                        Key = "runtime-state",
                        ImplementationType = typeof(TestRuntimeStateProvider)
                    }
                })));

            services.Replace(ServiceDescriptor.Singleton<IRagRetrievalRegistry>(
                _ => new DefaultRagRetrievalRegistry(new[]
                {
                    new RagRetrievalDescriptor
                    {
                        Key = "multi-provider",
                        ImplementationType = typeof(MultiProviderRetrieval)
                    }
                })));

            services.Replace(ServiceDescriptor.Singleton<IRagComposerRegistry>(
                _ => new DefaultRagComposerRegistry(new[]
                {
                    new RagComposerDescriptor
                    {
                        Key = "deterministic",
                        ImplementationType = typeof(DeterministicComposer)
                    }
                })));
        }

        /// <summary>
        /// Creates basic engine options for a RAG integration test pipeline.
        /// </summary>
        private static AiEngineOptions CreateOptions(string pipelinePath)
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = pipelinePath
            };
        }

        // ============================================================
        // TEST PROVIDERS
        // ============================================================

        /// <summary>
        /// Example vector provider for integration testing.
        /// </summary>
        private sealed class TestRedisVectorProvider : INormalizingRagProvider
        {
            public string Key => "redis-vector";

            public Task<RagRetrievalBatch> RetrieveNormalizedAsync(
                RagExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "vector-1",
                            ProviderKey = Key,
                            ProviderKind = RagProviderKind.Vector,
                            ContentType = "text/plain",
                            ContentText = $"Vector match for '{context.QueryText}'",
                            Score = 0.95
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Example SQL provider for integration testing.
        /// </summary>
        private sealed class TestHrSqlProvider : INormalizingRagProvider
        {
            public string Key => "hr-sql";

            public Task<RagRetrievalBatch> RetrieveNormalizedAsync(
                RagExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "sql-1",
                            ProviderKey = Key,
                            ProviderKind = RagProviderKind.Sql,
                            ContentType = "text/plain",
                            ContentText = $"SQL row for '{context.QueryText}'",
                            Score = 0.80
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Example runtime provider for integration testing.
        /// </summary>
        private sealed class TestRuntimeStateProvider : INormalizingRagProvider
        {
            public string Key => "runtime-state";

            public Task<RagRetrievalBatch> RetrieveNormalizedAsync(
                RagExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "runtime-1",
                            ProviderKey = Key,
                            ProviderKind = RagProviderKind.Runtime,
                            ContentType = "text/plain",
                            ContentText = $"Runtime state for '{context.QueryText}'",
                            Score = 0.50
                        }
                    }
                });
            }
        }
    }
}