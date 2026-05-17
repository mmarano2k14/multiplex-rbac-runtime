using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.MultipleInstance.Worker.Chaos
{
    public sealed record DistributedChaosScenario
    {
        public int StepCount { get; set; } = 100;
        public int WorkerCount { get; set; } = 10;
        public int MaxStepsPerCycle { get; set; } = 1;
        public int MaxWorkerCycles { get; set; } = 5000;
        public int MaxDegreeOfParallelism { get; set; } = 12;
        public int MaxProviderConcurrency { get; set; } = 3;
        public int MaxCompletedStepsInState { get; init; } = 15;
        public int FlakyStepInterval { get; init; } = 9;
        public TimeSpan IdleDelay { get; init; } = TimeSpan.FromMilliseconds(1);
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(180);

        public static DistributedChaosScenario Steps100()
        {
            return new DistributedChaosScenario();
        }

        public static DistributedChaosScenario Steps500()
        {
            return new DistributedChaosScenario
            {
                StepCount = 500,
                WorkerCount = 20,
                MaxWorkerCycles = 15000,
                MaxDegreeOfParallelism = 32,
                MaxProviderConcurrency = 6,
                MaxCompletedStepsInState = 30,
                Timeout = TimeSpan.FromSeconds(420)
            };
        }
    }
}
