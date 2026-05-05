using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Pipeline
{
    /// <summary>
    /// Integration tests for pipeline-level runtime configuration propagation.
    /// </summary>
    public sealed class AiExecutionPipelineConfigTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "ai-tests";

        /// <summary>
        /// Ensures pipeline-level configuration is copied into <see cref="AiExecutionState.PipelineConfig"/>
        /// when a DAG execution is created.
        /// </summary>
        [Fact]
        public async Task CreateAsync_Should_Copy_Pipeline_Config_Into_Execution_State()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddAiStepsFromAssemblies(
                        typeof(AiExecutionPipelineConfigTests).Assembly);
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "dag-pipeline-config",
                "input");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);
            Assert.True(state!.PipelineConfig.ContainsKey("retry"));

            var retryJson = JsonSerializer.Serialize(state.PipelineConfig["retry"]);
            using var document = JsonDocument.Parse(retryJson);

            Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
            Assert.Equal(3, document.RootElement.GetProperty("maxRetries").GetInt32());
            Assert.Equal("Fixed", document.RootElement.GetProperty("strategy").GetString());
        }

        /// <summary>
        /// Ensures step-level configuration overrides pipeline-level configuration
        /// when resolving config through the step context helper.
        /// </summary>
        [Fact]
        public async Task GetConfigAsync_Should_Use_Step_Config_Override_When_Pipeline_Config_Exists()
        {
            var options = CreateOptions();

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddAiStepsFromAssemblies(
                        typeof(AiExecutionPipelineConfigTests).Assembly);
                },
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "dag-pipeline-config",
                "input");

            await host.Engine.ExecuteNextAsync(created.ExecutionId);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);
            Assert.True(state!.Steps.TryGetValue("step-1", out var step));

            Assert.NotNull(step.Result);
            Assert.NotNull(step.Result!.Data);
            Assert.True(step.Result.Data.TryGetValue("delayMs", out var delayMs));

            var delayMsJson = Assert.IsType<JsonElement>(delayMs);
            Assert.Equal(100, delayMsJson.GetInt32());
        }

        /// <summary>
        /// Creates engine options using the same JSON file loading pattern as existing integration tests.
        /// </summary>
        private static AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\dag-pipeline-config.json"
            };
        }

        /// <summary>
        /// Test step that reads a config value through <see cref="IAiStepContextHelper"/>
        /// and returns it in the step result.
        /// </summary>
        [AiStep("pipeline-config-reader")]
        private sealed class PipelineConfigReaderStep : IAiStep
        {
            /// <inheritdoc />
            public string Name => "pipeline-config-reader";

            /// <inheritdoc />
            public async Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext ctx,
                CancellationToken ct = default)
            {
                var delayMs = await ctx
                    .GetHelper()
                    .GetConfigAsync<int>("delayMs", ct)
                    .ConfigureAwait(false);

                return AiStepResult.Ok(
                    value: null,
                    output: null,
                    data: new Dictionary<string, object?>
                    {
                        ["delayMs"] = delayMs
                    });
            }
        }
    }
}