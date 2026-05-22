using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos
{
    /// <summary>
    /// Creates distributed chaos pipeline definitions.
    /// </summary>
    public static class DistributedChaosPipelineFactory
    {
        /// <summary>
        /// Creates a distributed chaos pipeline definition.
        /// </summary>
        /// <param name="scenario">
        /// The distributed chaos scenario.
        /// </param>
        /// <returns>
        /// The pipeline definition.
        /// </returns>
        public static AiPipelineDefinition CreatePipelineDefinition(
            DistributedChaosScenario scenario)
        {
            ArgumentNullException.ThrowIfNull(
                scenario);

            var steps = new List<AiPipelineStepDefinition>
            {
                new()
                {
                    Name = "chaos-step-001",
                    StepKey = "hello-world",
                    Order = 1,
                    Config = CreateStepConfig(
                        scenario,
                        index: 1,
                        isFlaky: false)
                }
            };

            for (var index = 2; index < scenario.StepCount; index++)
            {
                var isFlaky =
                    index % scenario.FlakyStepInterval == 0;

                steps.Add(
                    new AiPipelineStepDefinition
                    {
                        Name = $"chaos-step-{index:000}",
                        StepKey = isFlaky
                            ? "distributed.chaos.flaky-provider"
                            : "hello-world",
                        Order = index,
                        DependsOn = new[] { "chaos-step-001" },
                        Config = CreateStepConfig(
                            scenario,
                            index,
                            isFlaky)
                    });
            }

            steps.Add(
                new AiPipelineStepDefinition
                {
                    Name = "final-join-step",
                    StepKey = "hello-world",
                    Order = scenario.StepCount,
                    DependsOn = Enumerable.Range(2, scenario.StepCount - 2)
                        .Select(index => $"chaos-step-{index:000}")
                        .ToArray(),
                    Config = new Dictionary<string, object?>
                    {
                        ["provider"] = "openai",
                        ["model"] = "gpt-4.1",
                        ["operation"] = "llm.compose",
                        ["delayMs"] = 5
                    }
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
                        ["maxProviderConcurrency"] = scenario.MaxProviderConcurrency,
                        ["leaseSeconds"] = 60,
                        ["defaultRetryAfterMs"] = 10,
                        ["jitter"] = false
                    },
                    ["retention"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["policies"] = new[]
                        {
                            "retention.compact.terminal",
                            "retention.evict.terminal"
                        },
                        ["archiveReason"] = scenario.RetentionArchiveReason,
                        ["trigger"] = new Dictionary<string, object?>
                        {
                            ["enabled"] = true,
                            ["maxStepsInState"] = scenario.MaxCompletedStepsInState,
                            ["maxCompletedStepsInState"] = scenario.MaxCompletedStepsInState,
                            ["maxInlinePayloadBytes"] = 1
                        }
                    }
                },
                Steps = steps
            };
        }

        /// <summary>
        /// Creates per-step configuration.
        /// </summary>
        /// <param name="scenario">
        /// The distributed chaos scenario.
        /// </param>
        /// <param name="index">
        /// The step index.
        /// </param>
        /// <param name="isFlaky">
        /// Whether the step fails once before succeeding.
        /// </param>
        /// <returns>
        /// The step configuration.
        /// </returns>
        private static Dictionary<string, object?> CreateStepConfig(
            DistributedChaosScenario scenario,
            int index,
            bool isFlaky)
        {
            ArgumentNullException.ThrowIfNull(
                scenario);

            var config = new Dictionary<string, object?>
            {
                ["provider"] = "openai",
                ["model"] = "gpt-4.1",
                ["operation"] = "llm.chat",
                ["delayMs"] = isFlaky ? 10 : 1
            };

            if (isFlaky)
            {
                config["attemptKey"] =
                    $"{scenario.PipelineName}:chaos-step-{index:000}";

                config["retry"] = new Dictionary<string, object?>
                {
                    ["maxRetries"] = 2,
                    ["strategy"] = "Fixed",
                    ["baseDelayMs"] = 15,
                    ["maxDelayMs"] = 15,
                    ["jitter"] = false
                };
            }

            return config;
        }
    }
}