using Microsoft.Extensions.DependencyInjection;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.DI;
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

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                engine.ExecuteAllAsync(created.ExecutionId));

            Assert.Contains("unknown-composer", ex.Message, StringComparison.OrdinalIgnoreCase);
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