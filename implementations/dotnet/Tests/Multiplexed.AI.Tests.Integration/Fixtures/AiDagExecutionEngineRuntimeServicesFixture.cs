using Multiplexed.AI.Runtime.Execution.Engine.Batch;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Creation;
using Multiplexed.AI.Runtime.Execution.Engine.Distributed;
using Multiplexed.AI.Runtime.Execution.Engine.Finalization;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Execution.Engine.Local;
using Multiplexed.AI.Runtime.Execution.Engine.Retention;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;

namespace Multiplexed.AI.Tests.Integration.Fixtures
{
    /// <summary>
    /// Creates composed DAG runtime services for tests.
    /// </summary>
    public static class AiDagExecutionEngineRuntimeServicesFixture
    {
        /// <summary>
        /// Creates composed DAG runtime services.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services.
        /// </param>
        /// <returns>
        /// The composed DAG runtime services.
        /// </returns>
        public static IAiDagExecutionEngineRuntimeServices Create(
            IAiDagExecutionEngineServices engineServices)
        {
            ArgumentNullException.ThrowIfNull(engineServices);

            var lifecycleHelper = new AiDagExecutionLifecycleHelper(
                engineServices);

            var retentionCoordinator = new AiDagRetentionCoordinator(
                engineServices);

            var claimService = new AiDagStepClaimService(
                engineServices);

            var claimedStepExecutor = new AiDagClaimedStepExecutor(
                engineServices);

            var finalizationService = new AiDagExecutionFinalizationService(
                engineServices,
                retentionCoordinator);

            var creator = new AiDagExecutionCreator(
                engineServices);

            var localRunner = new AiDagLocalExecutionRunner(
                engineServices,
                lifecycleHelper);

            var distributedRunner = new AiDagDistributedExecutionRunner(
                engineServices,
                claimService,
                claimedStepExecutor,
                retentionCoordinator,
                finalizationService,
                lifecycleHelper);

            var batchRunner = new AiDagBatchExecutionRunner(
                engineServices,
                claimService,
                claimedStepExecutor,
                finalizationService,
                lifecycleHelper);

            return new AiDagExecutionEngineRuntimeServices(
                creator,
                localRunner,
                distributedRunner,
                batchRunner,
                claimService,
                claimedStepExecutor,
                retentionCoordinator,
                finalizationService,
                lifecycleHelper);
        }
    }
}