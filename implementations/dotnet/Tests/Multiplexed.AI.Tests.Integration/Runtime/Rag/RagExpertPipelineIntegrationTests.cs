// File: RagExpertPipelineIntegrationTests.cs

using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.AI.Rag.Operations;
using Multiplexed.AI.Runtime.AI.Rag.Steps;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Results;
using Multiplexed.AI.Runtime.Rag.Operations;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    /// <summary>
    /// Validates a real expert RAG flow using dynamic retrieval operations plus merge + compose.
    ///
    /// FLOW:
    /// - rag.retrieval (getCandidate)
    /// - rag.retrieval (getJob)
    /// - custom.merge
    /// - rag.compose
    ///
    /// WHAT THIS TEST PROVES:
    /// - multiple dynamic operations can run in the same DAG
    /// - each operation receives a typed plugin context
    /// - cross-step data can be merged deterministically
    /// - compose consumes merged batch correctly
    /// - final context is persisted and readable
    /// </summary>
    public sealed class RagExpertPipelineIntegrationTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Run_Expert_Rag_Pipeline_EndToEnd()
        {
            await using var host = await CreateHostAsync();

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-expert-test",
                new Dictionary<string, object?>
                {
                    ["query"] = "Senior C# Engineer"
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            Assert.NotNull(final);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            var payloadResolver = host.ServiceProvider
                .GetRequiredService<IAiExecutionPayloadResolver>();

            Assert.NotNull(state);
            Assert.NotNull(state!.Steps);

            Assert.True(state.Steps["getCandidate"].IsCompleted);
            Assert.True(state.Steps["getJob"].IsCompleted);
            Assert.True(state.Steps["merge"].IsCompleted);
            Assert.True(state.Steps["compose"].IsCompleted);

            // ---------------------------------------------------------
            // Validate getCandidate output
            // ---------------------------------------------------------
            var candidateStep = state.Steps["getCandidate"];
            Assert.NotNull(candidateStep.Result);

            var candidateBatch = await candidateStep.Result!
                .GetRequiredDataAsync<RagRetrievalBatch>(
                    "batch",
                    payloadResolver);

            Assert.NotNull(candidateBatch.Items);
            Assert.Single(candidateBatch.Items);
            Assert.Equal("candidate-1", candidateBatch.Items[0].Id);

            // ---------------------------------------------------------
            // Validate getJob output
            // ---------------------------------------------------------
            var jobStep = state.Steps["getJob"];
            Assert.NotNull(jobStep.Result);

            var jobBatch = await jobStep.Result!
                .GetRequiredDataAsync<RagRetrievalBatch>(
                    "batch",
                    payloadResolver);

            Assert.NotNull(jobBatch.Items);
            Assert.Single(jobBatch.Items);
            Assert.Equal("job-1", jobBatch.Items[0].Id);

            // ---------------------------------------------------------
            // Validate merge output
            // ---------------------------------------------------------
            var mergeStep = state.Steps["merge"];
            Assert.NotNull(mergeStep.Result);

            var mergedBatch = await mergeStep.Result!
                .GetRequiredDataAsync<RagRetrievalBatch>(
                    "batch",
                    payloadResolver);

            Assert.NotNull(mergedBatch.Items);
            Assert.Equal(2, mergedBatch.Items.Count);

            var mergedIds = mergedBatch.Items
                .Select(x => x.Id)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { "candidate-1", "job-1" }, mergedIds);

            // ---------------------------------------------------------
            // Validate compose output
            // ---------------------------------------------------------
            var composeStep = state.Steps["compose"];
            Assert.NotNull(composeStep.Result);

            var context = await composeStep.Result!
                .GetRequiredDataAsync<RagStructuredContext>(
                    "context",
                    payloadResolver);

            var fragments = await composeStep.Result!
                .GetRequiredDataAsync<IReadOnlyList<RagContextFragment>>(
                    "fragments",
                    payloadResolver);

            Assert.NotNull(context.Text);
            Assert.NotNull(context.OrderedTexts);
            Assert.NotEmpty(context.OrderedTexts);
            Assert.NotEmpty(fragments);

            // Deterministic invariant
            for (var i = 0; i < fragments.Count; i++)
            {
                Assert.Equal(i, fragments[i].Order);
            }
        }

        // ============================================================
        // HOST SETUP
        // ============================================================

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync()
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\rag-expert-test.json"
            };

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddRagCore();

                    // Explicit registrations for the new rag.retrieval flow
                    services.AddTransient<IRagStepResultNormalizer, DefaultRagStepResultNormalizer>();
                    services.AddTransient<IRagRetrievalStepDispatcher, RagRetrievalStepDispatcher>();

                    // Register operations dynamically from this test assembly
                    services.AddAiStepsFromAssemblies(typeof(MergeRetrievalBatchStep).Assembly, typeof(AiRuntimeAssemblyMarker).Assembly);
                    services.AddRagFromAssemblies(typeof(GetCandidateOperation).Assembly, typeof(AiRuntimeAssemblyMarker).Assembly);

                    // Register custom merge step concrete type
                    //services.AddTransient<MergeRetrievalBatchStep>();
                });
        }

        // ============================================================
        // TEST OPERATIONS
        // ============================================================

        [RagOperation("getCandidate", "sql")]
        private sealed class GetCandidateOperation : RagOperationBase<AiExecutionContext>
        {
            public override string Key => "getCandidate";

            public override Task<RagRetrievalBatch> ExecuteAsync(
                IPluginExecutionContext<AiExecutionContext> context,
                CancellationToken cancellationToken)
            {
                Assert.NotNull(context.ExecutionContext);
                Assert.NotNull(context.Inputs);

                var query = context.Inputs.TryGetValue("query", out var queryValue)
                    ? queryValue?.ToString()
                    : null;

                return Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "candidate-1",
                            ProviderKey = "getCandidate",
                            ContentType = "text/plain",
                            ContentText = $"Candidate profile matched query '{query ?? string.Empty}'.",
                            StableOrder = 0
                        }
                    }
                });
            }
        }

        [RagOperation("getJob", "sql")]
        private sealed class GetJobOperation : RagOperationBase<AiExecutionContext>
        {
            public override string Key => "getJob";

            public override Task<RagRetrievalBatch> ExecuteAsync(
                IPluginExecutionContext<AiExecutionContext> context,
                CancellationToken cancellationToken)
            {
                Assert.NotNull(context.ExecutionContext);
                Assert.NotNull(context.Inputs);

                var query = context.Inputs.TryGetValue("query", out var queryValue)
                    ? queryValue?.ToString()
                    : null;

                return Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "job-1",
                            ProviderKey = "getJob",
                            ContentType = "text/plain",
                            ContentText = $"Job requirement matched query '{query ?? string.Empty}'.",
                            StableOrder = 0
                        }
                    }
                });
            }
        }

        // ============================================================
        // CUSTOM MERGE STEP
        // ============================================================

        /// <summary>
        /// Simple merge step used only for expert integration validation.
        ///
        /// PURPOSE:
        /// - Merge two retrieval batches from previous rag.retrieval steps
        /// - Return a serializable RagRetrievalBatch in the same result shape as other retrieval steps
        /// </summary>
        [AiStep("custom.merge")]
        private sealed class MergeRetrievalBatchStep : IAiStep
        {
            public string Name => "custom.merge";

            public async Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                ArgumentNullException.ThrowIfNull(context);

                var helper = context.GetHelper();

                var candidateBatch = await helper.GetRequiredBatchAsync(
                    "getCandidate",
                    cancellationToken).ConfigureAwait(false);

                var jobBatch = await helper.GetRequiredBatchAsync(
                    "getJob",
                    cancellationToken).ConfigureAwait(false);

                var mergedItems = candidateBatch.Items
                    .Concat(jobBatch.Items)
                    .OrderBy(x => x.Id, StringComparer.Ordinal)
                    .Select((item, index) =>
                    {
                        item.StableOrder = index;
                        return item;
                    })
                    .ToArray();

                var mergedBatch = new RagRetrievalBatch
                {
                    Items = mergedItems
                };

                return AiStepResult.Ok(
                    output: $"Merged retrieval batches with {mergedBatch.Items.Count} item(s).",
                    data: helper.ToDictionary(new
                    {
                        providerKey = "custom.merge",
                        itemCount = mergedBatch.Items.Count,
                        batch = mergedBatch,
                        diagnostics = mergedBatch.Diagnostics
                    }, ignoreNull: true));
            }
        }
    }
}