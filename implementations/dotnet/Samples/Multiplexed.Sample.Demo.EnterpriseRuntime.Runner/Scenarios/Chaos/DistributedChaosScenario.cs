using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos
{
    /// <summary>
    /// Represents a configurable distributed chaos scenario.
    /// </summary>
    public sealed class DistributedChaosScenario
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
        /// Gets or initializes the retention archive reason.
        /// </summary>
        public required string RetentionArchiveReason { get; init; }

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
        /// Gets or initializes the maximum provider concurrency.
        /// </summary>
        public int MaxProviderConcurrency { get; init; }

        /// <summary>
        /// Gets or initializes the maximum completed steps kept in hot state.
        /// </summary>
        public int MaxCompletedStepsInState { get; init; }

        /// <summary>
        /// Gets or initializes the flaky step interval.
        /// </summary>
        public int FlakyStepInterval { get; init; }

        /// <summary>
        /// Gets or initializes the minimum expected participating workers.
        /// </summary>
        public int MinimumExpectedParticipatingWorkers { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether all steps are fingerprinted.
        /// </summary>
        public bool FullStepFingerprint { get; init; } = true;

        /// <summary>
        /// Gets or initializes the worker idle delay.
        /// </summary>
        public TimeSpan WorkerIdleDelay { get; init; }

        /// <summary>
        /// Gets or initializes the execution timeout.
        /// </summary>
        public TimeSpan Timeout { get; init; }

        /// <summary>
        /// Gets or initializes the snapshot wait timeout.
        /// </summary>
        public TimeSpan SnapshotWaitTimeout { get; init; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or initializes the steps that must resolve after retention or replay.
        /// </summary>
        public IReadOnlyCollection<string> RequiredResolvedSteps { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Gets or initializes the steps expected to retry.
        /// </summary>
        public IReadOnlyCollection<string> ExpectedRetriedSteps { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Gets or initializes the steps included in replay fingerprinting.
        /// </summary>
        public IReadOnlyCollection<string> FingerprintStepNames { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Creates a 100-step distributed chaos scenario.
        /// </summary>
        /// <returns>
        /// The configured 100-step distributed chaos scenario.
        /// </returns>
        public static DistributedChaosScenario Steps100()
        {
            var pipelineName = $"distributed-chaos-100-{Guid.NewGuid():N}";

            var requiredSteps = new[]
            {
                "chaos-step-001",
                "chaos-step-009",
                "chaos-step-018",
                "chaos-step-090",
                "final-join-step"
            };

            var retriedSteps = Enumerable.Range(2, 98)
                .Where(index => index % 9 == 0)
                .Select(index => $"chaos-step-{index:000}")
                .ToArray();

            var scenario = new DistributedChaosScenario
            {
                Name = "distributed-chaos-100",
                PipelineName = pipelineName,
                CandidateId = "candidate-distributed-chaos-100",
                RetentionArchiveReason = "distributed-chaos-100-retention",
                StepCount = 100,
                WorkerCount = 10,
                MaxStepsPerCycle = 1,
                MaxWorkerCycles = 5000,
                MaxDegreeOfParallelism = 12,
                MaxProviderConcurrency = 3,
                MaxCompletedStepsInState = 15,
                FlakyStepInterval = 9,
                MinimumExpectedParticipatingWorkers = 3,
                FullStepFingerprint = true,
                WorkerIdleDelay = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(180),
                RequiredResolvedSteps = requiredSteps,
                ExpectedRetriedSteps = retriedSteps,
                FingerprintStepNames = requiredSteps
                    .Concat(retriedSteps)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };

            scenario.PipelineDefinition = DistributedChaosPipelineFactory.CreatePipelineDefinition(
                scenario);

            return scenario;
        }

        /// <summary>
        /// Creates a 500-step distributed chaos scenario.
        /// </summary>
        /// <returns>
        /// The configured 500-step distributed chaos scenario.
        /// </returns>
        public static DistributedChaosScenario Steps500()
        {
            var pipelineName = $"distributed-chaos-500-{Guid.NewGuid():N}";

            var requiredSteps = new[]
            {
                "chaos-step-001",
                "chaos-step-011",
                "chaos-step-022",
                "chaos-step-099",
                "chaos-step-250",
                "chaos-step-499",
                "final-join-step"
            };

            var retriedSteps = Enumerable.Range(2, 498)
                .Where(index => index % 11 == 0)
                .Select(index => $"chaos-step-{index:000}")
                .ToArray();

            var scenario = new DistributedChaosScenario
            {
                Name = "distributed-chaos-500",
                PipelineName = pipelineName,
                CandidateId = "candidate-distributed-chaos-500",
                RetentionArchiveReason = "distributed-chaos-500-retention",
                StepCount = 500,
                WorkerCount = 30,
                MaxStepsPerCycle = 5,
                MaxWorkerCycles = 10000,
                MaxDegreeOfParallelism = 64,
                MaxProviderConcurrency = 12,
                MaxCompletedStepsInState = 50,
                FlakyStepInterval = 11,
                MinimumExpectedParticipatingWorkers = 5,
                FullStepFingerprint = false,
                WorkerIdleDelay = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromMinutes(10),
                SnapshotWaitTimeout = TimeSpan.FromMinutes(4),
                RequiredResolvedSteps = requiredSteps,
                ExpectedRetriedSteps = retriedSteps,
                FingerprintStepNames = requiredSteps
                    .Concat(retriedSteps)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };

            scenario.PipelineDefinition =
                DistributedChaosPipelineFactory.CreatePipelineDefinition(
                    scenario);

            return scenario;
        }
    }
}