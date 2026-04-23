using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Stores;
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
    /// Validates a multi-entity expert RAG DAG using real external providers:
    ///
    /// - rag.retrieval (candidate.byId)
    /// - rag.retrieval (job.byId)
    /// - rag.merge
    /// - rag.compose
    ///
    /// PURPOSE:
    /// - Prove that the runtime is not candidate-centric.
    /// - Prove that multiple business operations can feed the standard merge step.
    /// - Prove that rag.compose can consume a merged multi-entity batch.
    /// </summary>
    public sealed class RagCandidateJobMergeIntegrationTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Merge_Candidate_And_Job_And_Compose_Context()
        {
            await using var host = await CreateHostAsync();

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "candidate-job-merge-test",
                new Dictionary<string, object?>
                {
                    ["candidateId"] = "cand-001",
                    ["jobId"] = "job-001",
                    ["candidateProviderKey"] = "sqlserver",
                    ["candidateExecutionMode"] = "provider",
                    ["jobProviderKey"] = "postgres",
                    ["jobExecutionMode"] = "provider"
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);
            Assert.NotNull(final);

            var (record, state) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.NotNull(record);
            Assert.NotNull(state);
            Assert.NotNull(state.Steps);

            Assert.True(state.Steps["candidate"].IsCompleted);
            Assert.True(state.Steps["job"].IsCompleted);
            Assert.True(state.Steps["merge"].IsCompleted);
            Assert.True(state.Steps["compose"].IsCompleted);

            // ---------------------------------------------------------
            // Validate candidate retrieval output
            // ---------------------------------------------------------
            var candidateStep = state.Steps["candidate"];
            Assert.NotNull(candidateStep.Result);
            Assert.NotNull(candidateStep.Result!.Data);

            Assert.True(
                candidateStep.Result.Data.TryGetValue("batch", out var candidateBatchValue),
                "Expected 'batch' in candidate step result data.");

            var candidateBatch = Assert.IsType<RagRetrievalBatch>(candidateBatchValue);
            Assert.NotNull(candidateBatch.Items);
            Assert.NotEmpty(candidateBatch.Items);

            // ---------------------------------------------------------
            // Validate job retrieval output
            // ---------------------------------------------------------
            var jobStep = state.Steps["job"];
            Assert.NotNull(jobStep.Result);
            Assert.NotNull(jobStep.Result!.Data);

            Assert.True(
                jobStep.Result.Data.TryGetValue("batch", out var jobBatchValue),
                "Expected 'batch' in job step result data.");

            var jobBatch = Assert.IsType<RagRetrievalBatch>(jobBatchValue);
            Assert.NotNull(jobBatch.Items);
            Assert.NotEmpty(jobBatch.Items);

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

            var itemCount = ExtractInt(itemCountValue, "itemCount");
            Assert.Equal(mergedBatch.Items.Count, itemCount);

            Assert.NotNull(diagnosticsValue);

            for (var i = 0; i < mergedBatch.Items.Count; i++)
            {
                Assert.Equal(i, mergedBatch.Items[i].StableOrder);
            }

            // Should contain at least one item from candidate and one from job paths.
            Assert.True(
                mergedBatch.Items.Count >= 2,
                "Expected merged batch to contain at least two items coming from candidate and job retrieval.");

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

            Assert.True(record.IsTerminal);
        }

        private static async Task<(AiExecutionRecord Record, AiExecutionState State)> LoadDistributedTruthAsync(
            IServiceProvider services,
            string executionId)
        {
            var dagStore = services.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(executionId);
            var state = await dagStore.GetStateAsync(executionId);

            Assert.NotNull(record);
            Assert.NotNull(state);

            return (record!, state!);
        }

        private static int ExtractInt(object? value, string fieldName)
        {
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number &&
                    jsonElement.TryGetInt32(out var jsonInt))
                {
                    return jsonInt;
                }

                if (jsonElement.ValueKind == JsonValueKind.String &&
                    int.TryParse(jsonElement.GetString(), out var parsed))
                {
                    return parsed;
                }
            }

            throw new InvalidOperationException(
                $"Could not extract integer field '{fieldName}' from value of type '{value?.GetType().FullName ?? "null"}'.");
        }

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync()
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\candidate-job-merge-test.json"
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