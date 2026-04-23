using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Sample.External.Plugins.Rag;
using Multiplexed.Sample.External.Plugins.Rag.DI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    /// <summary>
    /// Negative integration tests for rag.merge.
    ///
    /// PURPOSE:
    /// - Ensure merge fails clearly when a configured source step does not exist.
    /// - Ensure merge fails clearly when an upstream step does not expose a batch.
    /// </summary>
    public sealed class RagMergeNegativeIntegrationTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Fail_When_SourceStep_Does_Not_Exist()
        {
            await using var host = await CreateHostAsync(
                "config\\rag-merge-missing-source-test.json");

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-merge-missing-source-test",
                new Dictionary<string, object?>
                {
                    ["candidateId"] = "cand-001"
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.ExecuteAllAsync(created.ExecutionId));

            Assert.Contains("missing-step", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAllAsync_Should_Fail_When_SourceStep_Does_Not_Contain_Batch()
        {
            await using var host = await CreateHostAsync(
                "config\\rag-merge-missing-batch-test.json");

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-merge-missing-batch-test",
                new Dictionary<string, object?>());

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.ExecuteAllAsync(created.ExecutionId));

            Assert.Contains("batch", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fake-no-batch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync(string jsonPath)
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = jsonPath
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
                        typeof(AiRuntimeAssemblyMarker).Assembly,
                        typeof(RagMergeNegativeIntegrationTests).Assembly);

                    services.AddAiStepsFromAssemblies(typeof(RagMergeNegativeIntegrationTests).Assembly, typeof(AiRuntimeAssemblyMarker).Assembly);
                });
        }
    }

    /// <summary>
    /// Fake step that succeeds but does not return a batch.
    /// </summary>
    [AiStep("test.no-batch")]
    public sealed class NoBatchStep : IAiStep
    {
        public string Name => "test.no-batch";

        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AiStepResult.Ok(
                    output: "No batch returned.",
                    data: new Dictionary<string, object?>
                    {
                        ["value"] = "fake-no-batch"
                    }));
        }
    }
}