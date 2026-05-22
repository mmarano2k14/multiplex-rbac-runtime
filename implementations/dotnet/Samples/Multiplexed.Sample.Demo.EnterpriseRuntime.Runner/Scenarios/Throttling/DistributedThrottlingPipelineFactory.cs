using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Throttling
{
    /// <summary>
    /// Creates distributed throttling pipeline definitions.
    /// </summary>
    public static class DistributedThrottlingPipelineFactory
    {
        /// <summary>
        /// Creates a distributed throttling pipeline definition.
        /// </summary>
        /// <param name="scenario">The distributed throttling scenario.</param>
        /// <returns>The pipeline definition.</returns>
        public static AiPipelineDefinition CreatePipelineDefinition(
            DistributedThrottlingScenario scenario)
        {
            ArgumentNullException.ThrowIfNull(scenario);

            var steps = new List<AiPipelineStepDefinition>
            {
                new()
                {
                    Name = "throttling-root-step",
                    StepKey = "hello-world",
                    Order = 1,
                    Config = CreateStepConfig(scenario, delayMs: 200)
                }
            };

            for (var index = 2; index < scenario.StepCount; index++)
            {
                steps.Add(
                    new AiPipelineStepDefinition
                    {
                        Name = $"throttling-step-{index:000}",
                        StepKey = "hello-world",
                        Order = index,
                        DependsOn = new[] { "throttling-root-step" },
                        Config = CreateStepConfig(scenario, delayMs: 400)
                    });
            }

            steps.Add(
                new AiPipelineStepDefinition
                {
                    Name = "throttling-final-join-step",
                    StepKey = "hello-world",
                    Order = scenario.StepCount,
                    DependsOn = Enumerable.Range(2, scenario.StepCount - 2)
                        .Select(index => $"throttling-step-{index:000}")
                        .ToArray(),
                    Config = CreateStepConfig(scenario, delayMs: 250)
                });

            return new AiPipelineDefinition
            {
                Name = scenario.PipelineName,
                Version = "1.0.0",
                ExecutionMode = AiExecutionMode.Dag,
                Config = new Dictionary<string, object?>
                {
                    ["concurrency"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["maxDegreeOfParallelism"] = scenario.MaxDegreeOfParallelism,
                        ["jitter"] = false,
                        ["policies"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["name"] = "concurrency.throttle",
                                ["config"] = new Dictionary<string, object?>
                                {
                                    ["scope"] = "provider",
                                    ["target"] = scenario.Provider,
                                    ["limit"] = scenario.ProviderThrottleLimit,
                                    ["leaseSeconds"] = 60,
                                    ["defaultRetryAfterMs"] = 25
                                }
                            }
                        }
                    }
                },
                Steps = steps
            };
        }

        /// <summary>
        /// Creates a throttled step configuration.
        /// </summary>
        /// <param name="scenario">The distributed throttling scenario.</param>
        /// <param name="delayMs">The artificial step delay in milliseconds.</param>
        /// <returns>The step configuration.</returns>
        private static Dictionary<string, object?> CreateStepConfig(
            DistributedThrottlingScenario scenario,
            int delayMs)
        {
            ArgumentNullException.ThrowIfNull(scenario);

            var provider = ResolveProvider(
                scenario);

            return new Dictionary<string, object?>
            {
                ["provider"] = provider,
                ["model"] = ResolveModel(
                    scenario,
                    provider),
                ["operation"] = scenario.Operation,
                ["delayMs"] = delayMs,
                ["concurrency"] = new Dictionary<string, object?>
                {
                    ["enabled"] = true
                }
            };
        }

        /// <summary>
        /// Resolves a randomized provider while keeping OpenAI dominant enough to trigger throttling.
        /// </summary>
        /// <param name="scenario">The distributed throttling scenario.</param>
        /// <returns>The selected provider.</returns>
        private static string ResolveProvider(
            DistributedThrottlingScenario scenario)
        {
            ArgumentNullException.ThrowIfNull(scenario);

            var roll = Random.Shared.Next(100);

            if (roll < 70)
            {
                return scenario.Provider;
            }

            return roll switch
            {
                < 78 => "anthropic",
                < 86 => "google",
                < 94 => "mistral",
                _ => "cohere"
            };
        }

        /// <summary>
        /// Resolves the model name for the selected provider.
        /// </summary>
        /// <param name="scenario">The distributed throttling scenario.</param>
        /// <param name="provider">The selected provider.</param>
        /// <returns>The provider model name.</returns>
        private static string ResolveModel(
            DistributedThrottlingScenario scenario,
            string provider)
        {
            ArgumentNullException.ThrowIfNull(scenario);

            return provider switch
            {
                "openai" => scenario.Model,
                "anthropic" => "claude-sonnet",
                "google" => "gemini-pro",
                "mistral" => "mistral-large",
                "cohere" => "command-r",
                _ => scenario.Model
            };
        }
    }
}