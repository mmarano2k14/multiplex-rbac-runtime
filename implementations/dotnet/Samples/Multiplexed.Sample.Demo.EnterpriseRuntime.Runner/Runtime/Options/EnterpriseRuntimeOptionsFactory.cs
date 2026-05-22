using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Options
{
    /// <summary>
    /// Creates enterprise runtime demo engine options.
    /// </summary>
    public static class EnterpriseRuntimeOptionsFactory
    {
        /// <summary>
        /// Creates runtime options for the selected scenario.
        /// </summary>
        /// <param name="scenarioName">
        /// The scenario name.
        /// </param>
        /// <returns>
        /// The runtime options.
        /// </returns>
        public static AiEngineOptions Create(
            string scenarioName)
        {
            return scenarioName switch
            {
                EnterpriseRuntimeScenarioNames.Chaos100 =>
                    CreateChaos100(),

                EnterpriseRuntimeScenarioNames.Chaos500 =>
                    CreateChaos500(),

                _ =>
                    CreateJson()
            };
        }

        /// <summary>
        /// Creates JSON demo runtime options.
        /// </summary>
        /// <returns>
        /// The runtime options.
        /// </returns>
        private static AiEngineOptions CreateJson()
        {
            var options = CreateBaseOptions();

            options.DefaultPipelineDefinitionSource = "Runtime";

            options.RuntimeInstanceWorker.MaxStepsPerCycle = 2;
            options.RuntimeInstanceWorker.IdleDelay = TimeSpan.FromMilliseconds(5);
            options.RuntimeInstanceWorker.MaxCycles = 5000;

            options.PipelineBackgroundController.Distributed.WorkerCount = 3;
            options.PipelineBackgroundController.Distributed.TerminalObservationTimeout =
                TimeSpan.FromSeconds(30);

            options.Snapshots.Enabled = false;

            options.Cleanup.SuppressSnapshotIfExist = false;

            return options;
        }

        /// <summary>
        /// Creates 100-step distributed chaos runtime options.
        /// </summary>
        /// <returns>
        /// The runtime options.
        /// </returns>
        private static AiEngineOptions CreateChaos100()
        {
            var options = CreateChaosBase();

            options.RuntimeInstanceWorker.MaxCycles = 5000;

            options.PipelineBackgroundController.Distributed.WorkerCount = 10;

            return options;
        }

        /// <summary>
        /// Creates 500-step distributed chaos runtime options.
        /// </summary>
        /// <returns>
        /// The runtime options.
        /// </returns>
        private static AiEngineOptions CreateChaos500()
        {
            var options = CreateChaosBase();

            options.RuntimeInstanceWorker.MaxStepsPerCycle = 5;
            options.RuntimeInstanceWorker.MaxCycles = 20000;
            options.PipelineBackgroundController.Distributed.WorkerCount = 30;

            options.PipelineBackgroundController.Distributed.TerminalObservationTimeout =
                TimeSpan.FromMinutes(2);

            return options;
        }

        /// <summary>
        /// Creates shared distributed chaos runtime options.
        /// </summary>
        /// <returns>
        /// The runtime options.
        /// </returns>
        private static AiEngineOptions CreateChaosBase()
        {
            var options = CreateBaseOptions();

            options.DefaultPipelineDefinitionSource = "Runtime";

            options.RuntimeInstanceWorker.MaxStepsPerCycle = 1;
            options.RuntimeInstanceWorker.IdleDelay = TimeSpan.FromMilliseconds(1);

            options.Snapshots.Enabled = true;
            options.Snapshots.Mongo.Enabled = true;
            options.Snapshots.Mongo.ConnectionString = "mongodb://localhost:27017";
            options.Snapshots.Mongo.DatabaseName = "multiplexed_ai_tests";
            options.Snapshots.Mongo.CollectionName =
                $"execution_snapshots_distributed_chaos_{Guid.NewGuid():N}";

            options.Cleanup.SuppressSnapshotIfExist = true;


            return options;
        }

        /// <summary>
        /// Creates base runtime options shared by all demo profiles.
        /// </summary>
        /// <returns>
        /// The runtime options.
        /// </returns>
        private static AiEngineOptions CreateBaseOptions()
        {
            var options = new AiEngineOptions
            {
                RuntimeInstanceWorker = new AiRuntimeInstanceWorkerOptions
                {
                    IgnoreConcurrencyConflicts = true
                },

                PipelineBackgroundController = new AiRuntimePipelineBackgroundControllerOptions
                {
                    MaxConcurrentRuns = 1,
                    QueueCapacity = 8,
                    RejectEnqueueWhenStopped = false,
                    StopOnFirstFailure = false,

                    Distributed = new AiRuntimeDistributedExecutionOptions
                    {
                        Enabled = true,
                        StopOnFirstTerminal = true
                    }
                }
            };

            options.Observability.EnableTracing = true;
            options.Observability.EnableInMemoryRecording = true;

            options.Cleanup.AutoCleanupOnCompleted = false;
            options.Cleanup.AutoCleanupOnFailed = false;
            options.Cleanup.SuppressCleanupExceptions = true;

            return options;
        }
    }
}