using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution
{
    /// <summary>
    /// Creates enterprise runtime execution requests.
    /// </summary>
    public static class EnterpriseRuntimeExecutionRequestFactory
    {
        /// <summary>
        /// Creates the standard JSON demo execution request.
        /// </summary>
        /// <param name="scenarioName">
        /// The scenario name.
        /// </param>
        /// <param name="pipelineFilePath">
        /// The pipeline JSON file path.
        /// </param>
        /// <returns>
        /// The execution request.
        /// </returns>
        public static EnterpriseRuntimeExecutionRequest CreateJsonDemo(
            string scenarioName,
            string pipelineFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                scenarioName);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                pipelineFilePath);

            return new EnterpriseRuntimeExecutionRequest
            {
                ScenarioName = scenarioName,
                PipelineName = "enterprise-runtime-demo",
                PipelineInput = EnterpriseRuntimePipelineInput.FromJsonFilePath(
                    pipelineFilePath),
                Input = new
                {
                    source = scenarioName,
                    demo = true
                },
                ExpectedCompletedStepCount = 12,
                MinimumWorkerCount = 2,
                RetriedStepName = "transient-provider-call",
                ExpectRetryRecovery = true,
                CleanupExecutionBundle = true,
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        /// <summary>
        /// Creates a distributed chaos execution request.
        /// </summary>
        /// <param name="scenario">
        /// The distributed chaos scenario.
        /// </param>
        /// <returns>
        /// The execution request.
        /// </returns>
        public static EnterpriseRuntimeExecutionRequest CreateChaosDemo(
            DistributedChaosScenario scenario)
        {
            ArgumentNullException.ThrowIfNull(
                scenario);

            return new EnterpriseRuntimeExecutionRequest
            {
                ScenarioName = scenario.Name,
                PipelineName = scenario.PipelineName,
                PipelineInput = EnterpriseRuntimePipelineInput.FromDefinition(
                    scenario.PipelineDefinition),
                Input = new
                {
                    candidateId = scenario.CandidateId,
                    source = scenario.Name,
                    stepCount = scenario.StepCount,
                    workerCount = scenario.WorkerCount,
                    chaos = true
                },
                ExpectedCompletedStepCount = scenario.StepCount,
                MinimumWorkerCount = scenario.MinimumExpectedParticipatingWorkers,
                RetriedStepName = scenario.ExpectedRetriedSteps.FirstOrDefault(),
                ExpectRetryRecovery = scenario.ExpectedRetriedSteps.Count > 0,
                ExpectedRetriedStepNames = scenario.ExpectedRetriedSteps,
                CleanupExecutionBundle = true,
                Timeout = scenario.Timeout,
                ValidateReplay = true,
                ChaosScenario = scenario,
                MaxHotStateStepCount = scenario.MaxCompletedStepsInState
            };
        }
    }
}