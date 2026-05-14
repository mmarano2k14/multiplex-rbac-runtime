using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Sample.External.Plugins.Rag;
using Multiplexed.Sample.External.Plugins.Rag.DI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    /// <summary>
    /// Negative integration tests for rag.compose.
    ///
    /// PURPOSE:
    /// - Ensure compose fails clearly when the configured composer cannot be resolved.
    /// </summary>
    public sealed class RagComposeNegativeIntegrationTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Fail_When_Composer_Is_Not_Registered()
        {
            await using var host = await CreateHostAsync(
                "config\\rag-compose-invalid-composer-test.json");

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-compose-invalid-composer-test",
                new Dictionary<string, object?>
                {
                    ["candidateId"] = "cand-001"
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            AiExecutionRecord result = created;

            for (var attempt = 0; attempt < 50; attempt++)
            {
                result = await engine.ExecuteAllAsync(created.ExecutionId);

                if (result.IsTerminal)
                {
                    break;
                }

                await Task.Delay(25);
            }

            Assert.True(
                result.IsTerminal,
                $"Execution did not reach a terminal state. LastStatus='{result.Status}'.");

            Assert.Equal(AiExecutionStatus.Failed, result.Status);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);

            var failedSteps = state!.Steps.Values
                .Where(step => step.Status == AiStepExecutionStatus.Failed)
                .ToList();

            Assert.NotEmpty(failedSteps);

            Assert.Contains(
                failedSteps,
                step => !string.IsNullOrWhiteSpace(step.Error) &&
                        step.Error.Contains("unknown-composer", StringComparison.OrdinalIgnoreCase));
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
                        typeof(AiRuntimeAssemblyMarker).Assembly);
                });
        }
    }
}