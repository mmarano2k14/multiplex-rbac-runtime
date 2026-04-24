using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Steps;
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
    ///
    /// IMPORTANT:
    /// - Step results may be payload-compacted by the DAG engine.
    /// - Large values may appear as summaries in Data and as full content in DataPayloads.
    /// - This test reads values through IAiExecutionPayloadResolver when needed.
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

            var payloadResolver =
                host.ServiceProvider.GetRequiredService<IAiExecutionPayloadResolver>();

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

            var candidateBatch = await GetResultDataValueAsync<RagRetrievalBatch>(
                candidateStep.Result!,
                "batch",
                payloadResolver);

            Assert.NotNull(candidateBatch);
            Assert.NotNull(candidateBatch!.Items);
            Assert.NotEmpty(candidateBatch.Items);

            // ---------------------------------------------------------
            // Validate job retrieval output
            // ---------------------------------------------------------
            var jobStep = state.Steps["job"];
            Assert.NotNull(jobStep.Result);

            var jobBatch = await GetResultDataValueAsync<RagRetrievalBatch>(
                jobStep.Result!,
                "batch",
                payloadResolver);

            Assert.NotNull(jobBatch);
            Assert.NotNull(jobBatch!.Items);
            Assert.NotEmpty(jobBatch.Items);

            // ---------------------------------------------------------
            // Validate merge output
            // ---------------------------------------------------------
            var mergeStep = state.Steps["merge"];
            Assert.NotNull(mergeStep.Result);

            var mergedBatch = await GetResultDataValueAsync<RagRetrievalBatch>(
                mergeStep.Result!,
                "batch",
                payloadResolver);

            var itemCountValue = await GetResultDataValueAsync<object>(
                mergeStep.Result!,
                "itemCount",
                payloadResolver);

            var diagnosticsValue = await GetResultDataValueAsync<object>(
                mergeStep.Result!,
                "diagnostics",
                payloadResolver);

            Assert.NotNull(mergedBatch);
            Assert.NotNull(mergedBatch!.Items);
            Assert.NotEmpty(mergedBatch.Items);

            var itemCount = ExtractInt(itemCountValue, "itemCount");
            Assert.Equal(mergedBatch.Items.Count, itemCount);

            Assert.NotNull(diagnosticsValue);

            for (var i = 0; i < mergedBatch.Items.Count; i++)
            {
                Assert.Equal(i, mergedBatch.Items[i].StableOrder);
            }

            Assert.True(
                mergedBatch.Items.Count >= 2,
                "Expected merged batch to contain at least two items coming from candidate and job retrieval.");

            // ---------------------------------------------------------
            // Validate compose output
            // ---------------------------------------------------------
            var composeStep = state.Steps["compose"];
            Assert.NotNull(composeStep.Result);

            var context = await GetResultDataValueAsync<RagStructuredContext>(
                composeStep.Result!,
                "context",
                payloadResolver);

            var fragments = await GetResultDataValueAsync<IReadOnlyList<RagContextFragment>>(
                composeStep.Result!,
                "fragments",
                payloadResolver);

            Assert.NotNull(context);
            Assert.NotNull(context!.Text);
            Assert.NotNull(context.OrderedTexts);
            Assert.NotEmpty(context.OrderedTexts);

            Assert.NotNull(fragments);
            Assert.NotEmpty(fragments!);

            for (var i = 0; i < fragments.Count; i++)
            {
                Assert.Equal(i, fragments[i].Order);
            }

            Assert.True(record.IsTerminal);
        }

        private static async Task<T?> GetResultDataValueAsync<T>(
            AiStepResult result,
            string key,
            IAiExecutionPayloadResolver payloadResolver)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(payloadResolver);

            object? raw = null;

            if (result.DataPayloads is not null &&
                result.DataPayloads.TryGetValue(key, out var payload))
            {
                raw = await payloadResolver.ResolveAsync(payload);
            }
            else if (result.Data is not null &&
                     result.Data.TryGetValue(key, out var value))
            {
                raw = value;
            }

            if (raw is null)
            {
                return default;
            }

            return ConvertValue<T>(raw);
        }

        private static T? ConvertValue<T>(object raw)
        {
            if (raw is T typed)
            {
                return typed;
            }

            if (raw is JsonElement json)
            {
                if (typeof(T) == typeof(string))
                {
                    return (T?)(object?)(
                        json.ValueKind == JsonValueKind.String
                            ? json.GetString()
                            : json.GetRawText());
                }

                if (typeof(T) == typeof(object))
                {
                    return (T?)(object?)json;
                }

                return json.Deserialize<T>();
            }

            if (typeof(T) == typeof(string))
            {
                return (T?)(object?)raw.ToString();
            }

            if (typeof(T) == typeof(object))
            {
                return (T?)(object?)raw;
            }

            var serialized = JsonSerializer.Serialize(raw);
            return JsonSerializer.Deserialize<T>(serialized);
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

            if (int.TryParse(value?.ToString(), out var parsedValue))
            {
                return parsedValue;
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