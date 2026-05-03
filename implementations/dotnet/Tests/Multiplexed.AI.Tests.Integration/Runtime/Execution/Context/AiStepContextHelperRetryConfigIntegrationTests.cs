using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.State;
using Xunit;
using static Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures.AiDagExecutionEngineTestHost;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Context
{
    public sealed class AiStepContextHelperRetryConfigIntegrationTests
    {
        [Fact]
        public async Task GetConfigAsync_Should_Return_Null_When_Retry_Config_Is_Missing()
        {
            var stepContext = CreateStepContext(
                new Dictionary<string, object?>());

            var result = await stepContext
                .GetHelper()
                .GetConfigAsync<AiRetryPolicyDefinition>("retry");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetConfigAsync_Should_Resolve_Retry_Config_With_Multiple_Policies()
        {
            var stepContext = CreateStepContext(
                new Dictionary<string, object?>
                {
                    ["retry"] = new Dictionary<string, object?>
                    {
                        ["policies"] = new[]
                        {
                            "retry.transient.redis",
                            "retry.transient.llm"
                        },
                        ["maxRetries"] = 5,
                        ["strategy"] = "exponential",
                        ["baseDelayMs"] = 200,
                        ["maxDelayMs"] = 5000,
                        ["jitter"] = true
                    }
                });

            var result = await stepContext
                .GetHelper()
                .GetConfigAsync<AiRetryPolicyDefinition>("retry");

            Assert.NotNull(result);
            Assert.Equal(
                new[] { "retry.transient.redis", "retry.transient.llm" },
                result!.Policies);
            Assert.Equal(5, result.MaxRetries);
            Assert.Equal(AiRetryBackoffStrategy.Exponential, result.Strategy);
            Assert.Equal(200, result.BaseDelayMs);
            Assert.Equal(5000, result.MaxDelayMs);
            Assert.True(result.Jitter);
        }

        [Fact]
        public async Task GetConfigAsync_Should_Resolve_Retry_Config_With_Single_Policy()
        {
            var stepContext = CreateStepContext(
                new Dictionary<string, object?>
                {
                    ["retry"] = new Dictionary<string, object?>
                    {
                        ["policies"] = new[] { "retry.transient.default" }
                    }
                });

            var result = await stepContext
                .GetHelper()
                .GetConfigAsync<AiRetryPolicyDefinition>("retry");

            Assert.NotNull(result);
            Assert.Equal(new[] { "retry.transient.default" }, result!.Policies);
            Assert.Equal(3, result.MaxRetries);
            Assert.Equal(AiRetryBackoffStrategy.Fixed, result.Strategy);
            Assert.Equal(500, result.BaseDelayMs);
            Assert.Null(result.MaxDelayMs);
            Assert.False(result.Jitter);
        }

        private static AiStepExecutionContext CreateStepContext(
            IReadOnlyDictionary<string, object?> config)
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAiContextValueResolver, DefaultAiContextValueResolver>();

            var provider = services.BuildServiceProvider();

            var record = new AiExecutionRecord
            {
                ExecutionId = "execution-1",
                PipelineName = "test-pipeline"
            };

            var state = new AiExecutionState
            {
                ExecutionId = "execution-1"
            };

            IAiExecutionStateReader stateReader =
                new DefaultAiExecutionStateReader(new NoopPayloadResolver());

            IAiExecutionStateWriter stateWriter =
                new DefaultAiExecutionStateWriter();

            var executionContext = new AiExecutionContext(
                record,
                state,
                provider,
                stateReader,
                stateWriter,
                CancellationToken.None);

            var step = new ResolvedAiPipelineStep
            {
                Name = "step-1",
                StepKey = "test.step",
                Config = config
            };

            return new AiStepExecutionContext(
                executionContext,
                step);
        }
    }
}