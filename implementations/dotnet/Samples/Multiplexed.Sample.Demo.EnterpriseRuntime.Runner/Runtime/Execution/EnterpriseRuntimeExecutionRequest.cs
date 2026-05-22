using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution
{
    /// <summary>
    /// Represents an enterprise runtime execution request.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionRequest
    {
        /// <summary>
        /// Gets or initializes the scenario name.
        /// </summary>
        public required string ScenarioName { get; init; }

        /// <summary>
        /// Gets or initializes the pipeline name.
        /// </summary>
        public required string PipelineName { get; init; }

        /// <summary>
        /// Gets or initializes the pipeline input.
        /// </summary>
        public required EnterpriseRuntimePipelineInput PipelineInput { get; init; }

        /// <summary>
        /// Gets or initializes the expected completed step count.
        /// </summary>
        public int? ExpectedCompletedStepCount { get; init; }

        /// <summary>
        /// Gets or initializes the minimum expected distributed worker count.
        /// </summary>
        public int MinimumWorkerCount { get; init; } = 1;

        /// <summary>
        /// Gets or initializes the retried step name.
        /// </summary>
        public string? RetriedStepName { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether retry recovery is expected.
        /// </summary>
        public bool ExpectRetryRecovery { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether the execution bundle should be cleaned up.
        /// </summary>
        public bool CleanupExecutionBundle { get; init; } = true;

        /// <summary>
        /// Gets or initializes the execution timeout.
        /// </summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or initializes the runtime input payload.
        /// </summary>
        public object? Input { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether replay validation should be executed.
        /// </summary>
        public bool ValidateReplay { get; init; }

        /// <summary>
        /// Gets or initializes the distributed chaos scenario used for replay validation.
        /// </summary>
        public DistributedChaosScenario? ChaosScenario { get; init; }

        /// <summary>
        /// Gets or initializes the expected retried step names.
        /// </summary>
        public IReadOnlyCollection<string> ExpectedRetriedStepNames { get; init; } =
            Array.Empty<string>();

        /// <summary>
        /// Gets or initializes the maximum configured hot state step count.
        /// </summary>
        public int? MaxHotStateStepCount { get; init; }
    }
}