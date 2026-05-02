using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.Execution.State;

namespace Multiplexed.AI.Tests.Runtime.Execution.State
{
    public sealed class DefaultAiExecutionStateWriterRetryTests
    {
        [Fact]
        public void EnsureStepInitialized_Should_Hydrate_Retry_Definition()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAiRetryPolicyDefinitionResolver, DefaultAiRetryPolicyDefinitionResolver>();
            services.AddSingleton<IAiExecutionStateWriter, DefaultAiExecutionStateWriter>();

            var provider = services.BuildServiceProvider();

            var writer = provider.GetRequiredService<IAiExecutionStateWriter>();

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                PipelineName = "test"
            };

            var step = new ResolvedAiPipelineStep
            {
                Name = "compose",
                Order = 1,
                DependsOn = new List<string>(),
                Config = new Dictionary<string, object?>
                {
                    ["retry"] = new Dictionary<string, object?>
                    {
                        ["policy"] = "retry.transient.default",
                        ["maxRetries"] = 5,
                        ["strategy"] = "exponential",
                        ["baseDelayMs"] = 200
                    }
                }
            };

            writer.EnsureStepInitialized(state, step);

            var stepState = state.Steps["compose"];

            Assert.NotNull(stepState.Retry);
            Assert.NotNull(stepState.RetryState);

            Assert.Equal(5, stepState.Retry.MaxRetries);
            Assert.Equal(AiRetryBackoffStrategy.Exponential, stepState.Retry.Strategy);

#pragma warning disable CS0618
            Assert.Equal(step.MaxRetries, stepState.MaxRetries);
#pragma warning restore CS0618
        }
    }
}