using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Helpers;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Sample.External.Plugins.Rag;
using Multiplexed.Sample.External.Plugins.Rag.DI;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
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

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(created.ExecutionId);
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            var payloadResolver =
                host.ServiceProvider.GetRequiredService<IAiExecutionPayloadResolver>();

            Assert.True(state!.Steps["candidate-sqlserver"].IsCompleted);
            Assert.True(state.Steps["candidate-postgres"].IsCompleted);
            Assert.True(state.Steps["merge"].IsCompleted);
            Assert.True(state.Steps["compose"].IsCompleted);

            // ---------------- SQL ----------------
            var sqlBatch = await GetDataAsync<RagRetrievalBatch>(
                state.Steps["candidate-sqlserver"].Result!,
                "batch",
                payloadResolver);

            Assert.NotNull(sqlBatch);
            Assert.NotEmpty(sqlBatch!.Items);

            // ---------------- Postgres ----------------
            var postgresBatch = await GetDataAsync<RagRetrievalBatch>(
                state.Steps["candidate-postgres"].Result!,
                "batch",
                payloadResolver);

            Assert.NotNull(postgresBatch);
            Assert.NotEmpty(postgresBatch!.Items);

            // ---------------- Merge ----------------
            var mergeResult = state.Steps["merge"].Result!;

            var mergedBatch = await GetDataAsync<RagRetrievalBatch>(
                mergeResult,
                "batch",
                payloadResolver);

            var itemCountValue = await GetDataAsync<object>(
                mergeResult,
                "itemCount",
                payloadResolver);

            var diagnosticsValue = await GetDataAsync<object>(
                mergeResult,
                "diagnostics",
                payloadResolver);

            Assert.NotNull(mergedBatch);
            Assert.NotEmpty(mergedBatch!.Items);

            var itemCount = AiResultDataAssertions.ExtractInt(itemCountValue, "itemCount");
            Assert.Equal(mergedBatch.Items.Count, itemCount);

            for (var i = 0; i < mergedBatch.Items.Count; i++)
            {
                Assert.Equal(i, mergedBatch.Items[i].StableOrder);
            }

            // ---------------- Compose ----------------
            var composeResult = state.Steps["compose"].Result!;

            var context = await GetDataAsync<RagStructuredContext>(
                composeResult,
                "context",
                payloadResolver);

            var fragments = await GetDataAsync<IReadOnlyList<RagContextFragment>>(
                composeResult,
                "fragments",
                payloadResolver);

            Assert.NotNull(context);
            Assert.NotEmpty(context!.OrderedTexts);

            Assert.NotNull(fragments);
            Assert.NotEmpty(fragments!);

            for (var i = 0; i < fragments.Count; i++)
            {
                Assert.Equal(i, fragments[i].Order);
            }
        }

        // ===============================
        // PAYLOAD HELPER
        // ===============================

        private static async Task<T?> GetDataAsync<T>(
            AiStepResult result,
            string key,
            IAiExecutionPayloadResolver resolver)
        {
            object? raw = null;

            if (result.DataPayloads != null &&
                result.DataPayloads.TryGetValue(key, out var payload))
            {
                raw = await resolver.ResolveAsync(payload);
            }
            else if (result.Data != null &&
                     result.Data.TryGetValue(key, out var value))
            {
                raw = value;
            }

            if (raw is null)
                return default;

            return Convert<T>(raw);
        }

        private static T? Convert<T>(object raw)
        {
            if (raw is T t)
                return t;

            if (raw is JsonElement json)
                return json.Deserialize<T>();

            return JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(raw));
        }

        // ===============================
        // HOST
        // ===============================

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync()
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\rag-external-merge-test.json",
                Snapshots = new AiExecutionSnapshotOptions { Enabled = false },
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