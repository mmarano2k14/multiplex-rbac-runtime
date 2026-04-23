using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Helpers;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Sample.External.Plugins.Rag;
using Multiplexed.Sample.External.Plugins.Rag.DI;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    /// <summary>
    /// Validates the full expert external-provider RAG path:
    ///
    /// - rag.retrieval (SQL Server provider mode)
    /// - rag.retrieval (Postgres provider mode)
    /// - rag.merge
    /// - rag.compose
    ///
    /// PURPOSE:
    /// - Prove that real external providers can feed the standard runtime merge step.
    /// - Prove that rag.compose can consume the merged batch without any custom test step.
    /// - Close the gap between external provider integration tests and runtime expert DAG tests.
    /// </summary>
    public sealed class RagMergeExternalProvidersIntegrationTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Merge_External_Providers_And_Compose_Context()
        {
            await using var host = await CreateHostAsync();

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-external-merge-test",
                new Dictionary<string, object?>
                {
                    ["candidateIdSqlServer"] = "cand-001",
                    ["candidateIdPostgres"] = "cand-101"
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);
            Assert.NotNull(final);


            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(created.ExecutionId);
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(record);
            Assert.NotNull(state);

            Assert.NotNull(state!.Steps);

            Assert.True(state.Steps["candidate-sqlserver"].IsCompleted);
            Assert.True(state.Steps["candidate-postgres"].IsCompleted);
            Assert.True(state.Steps["merge"].IsCompleted);
            Assert.True(state.Steps["compose"].IsCompleted);

            // ---------------------------------------------------------
            // Validate SQL Server retrieval output
            // ---------------------------------------------------------
            var sqlStep = state.Steps["candidate-sqlserver"];
            Assert.NotNull(sqlStep.Result);
            Assert.NotNull(sqlStep.Result!.Data);
            Assert.True(
                sqlStep.Result.Data.TryGetValue("batch", out var sqlBatchValue),
                "Expected 'batch' in candidate-sqlserver result data.");

            var sqlBatch = Assert.IsType<RagRetrievalBatch>(sqlBatchValue);
            Assert.NotNull(sqlBatch.Items);
            Assert.NotEmpty(sqlBatch.Items);

            // ---------------------------------------------------------
            // Validate Postgres retrieval output
            // ---------------------------------------------------------
            var postgresStep = state.Steps["candidate-postgres"];
            Assert.NotNull(postgresStep.Result);
            Assert.NotNull(postgresStep.Result!.Data);
            Assert.True(
                postgresStep.Result.Data.TryGetValue("batch", out var postgresBatchValue),
                "Expected 'batch' in candidate-postgres result data.");

            var postgresBatch = Assert.IsType<RagRetrievalBatch>(postgresBatchValue);
            Assert.NotNull(postgresBatch.Items);
            Assert.NotEmpty(postgresBatch.Items);

            // ---------------------------------------------------------
            // Validate merge output
            // ---------------------------------------------------------
            var mergeStep = state.Steps["merge"];
            Assert.NotNull(mergeStep.Result);
            Assert.NotNull(mergeStep.Result!.Data);

            Assert.True(
                mergeStep.Result.Data.TryGetValue("batch", out var mergedBatchValue),
                "Expected 'batch' in rag.merge result data.");

            Assert.True(
                mergeStep.Result.Data.TryGetValue("itemCount", out var itemCountValue),
                "Expected 'itemCount' in rag.merge result data.");

            Assert.True(
                mergeStep.Result.Data.TryGetValue("diagnostics", out var diagnosticsValue),
                "Expected 'diagnostics' in rag.merge result data.");

            var mergedBatch = Assert.IsType<RagRetrievalBatch>(mergedBatchValue);
            Assert.NotNull(mergedBatch.Items);
            Assert.NotEmpty(mergedBatch.Items);

            var itemCount = AiResultDataAssertions.ExtractInt(itemCountValue, "itemCount");
            Assert.Equal(mergedBatch.Items.Count, itemCount);

            Assert.NotNull(diagnosticsValue);

            for (var i = 0; i < mergedBatch.Items.Count; i++)
            {
                Assert.Equal(i, mergedBatch.Items[i].StableOrder);
            }

            // ---------------------------------------------------------
            // Validate compose output
            // ---------------------------------------------------------
            var composeStep = state.Steps["compose"];
            Assert.NotNull(composeStep.Result);
            Assert.NotNull(composeStep.Result!.Data);

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
            Assert.NotEmpty(context.OrderedTexts);

            Assert.NotNull(fragments);
            Assert.NotEmpty(fragments);

            for (var i = 0; i < fragments.Count; i++)
            {
                Assert.Equal(i, fragments[i].Order);
            }
        }

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync()
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\rag-external-merge-test.json",
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
                    //services.AddMultiplexAI(options);

                    // Start with the simplest reproducible setup.
                    // Once green, you can clone this test for EF mode if needed.
                    services.AddExternalSqlServerInMemory();
                    services.AddExternalPostgresInMemory();
                    services.AddExternalRag();

                    services.AddRagFromAssemblies(
                        typeof(RagPluginsAssemblyMarker).Assembly,
                        typeof(AiRuntimeAssemblyMarker).Assembly);
                });
        }
    }
}