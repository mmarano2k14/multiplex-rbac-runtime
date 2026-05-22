using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Throttling
{
    /// <summary>
    /// Represents a distributed throttling demo scenario.
    /// </summary>
    public sealed class DistributedThrottlingScenario
    {
        /// <summary>
        /// Gets or initializes the scenario name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or initializes the pipeline name.
        /// </summary>
        public required string PipelineName { get; init; }

        /// <summary>
        /// Gets or initializes the candidate identifier.
        /// </summary>
        public required string CandidateId { get; init; }

        /// <summary>
        /// Gets or sets the in-memory pipeline definition.
        /// </summary>
        public AiPipelineDefinition PipelineDefinition { get; set; } = null!;

        /// <summary>
        /// Gets or initializes the total step count.
        /// </summary>
        public int StepCount { get; init; }

        /// <summary>
        /// Gets or initializes the distributed worker count.
        /// </summary>
        public int WorkerCount { get; init; }

        /// <summary>
        /// Gets or initializes the maximum steps per worker cycle.
        /// </summary>
        public int MaxStepsPerCycle { get; init; }

        /// <summary>
        /// Gets or initializes the maximum worker cycles.
        /// </summary>
        public int MaxWorkerCycles { get; init; }

        /// <summary>
        /// Gets or initializes the maximum DAG parallelism.
        /// </summary>
        public int MaxDegreeOfParallelism { get; init; }

        /// <summary>
        /// Gets or initializes the provider throttle limit.
        /// </summary>
        public int ProviderThrottleLimit { get; init; }

        /// <summary>
        /// Gets or initializes the provider name.
        /// </summary>
        public required string Provider { get; init; }

        /// <summary>
        /// Gets or initializes the model name.
        /// </summary>
        public required string Model { get; init; }

        /// <summary>
        /// Gets or initializes the operation name.
        /// </summary>
        public required string Operation { get; init; }

        /// <summary>
        /// Gets or initializes the worker idle delay.
        /// </summary>
        public TimeSpan WorkerIdleDelay { get; init; }

        /// <summary>
        /// Gets or initializes the execution timeout.
        /// </summary>
        public TimeSpan Timeout { get; init; }

        /// <summary>
        /// Creates the 100-step distributed throttling scenario.
        /// </summary>
        /// <returns>The configured distributed throttling scenario.</returns>
        public static DistributedThrottlingScenario Steps100()
        {
            var pipelineName = $"distributed-throttling-100-{Guid.NewGuid():N}";

            var scenario = new DistributedThrottlingScenario
            {
                Name = "distributed-throttling-100",
                PipelineName = pipelineName,
                CandidateId = "candidate-distributed-throttling-100",
                StepCount = 100,
                WorkerCount = 30,
                MaxStepsPerCycle = 5,
                MaxWorkerCycles = 10000,
                MaxDegreeOfParallelism = 64,
                ProviderThrottleLimit = 3,
                Provider = "openai",
                Model = "gpt-4.1",
                Operation = "llm.chat",
                WorkerIdleDelay = TimeSpan.FromMilliseconds(300),
                Timeout = TimeSpan.FromMinutes(5)
            };

            scenario.PipelineDefinition = DistributedThrottlingPipelineFactory.CreatePipelineDefinition(
                scenario);

            return scenario;
        }
    }
}