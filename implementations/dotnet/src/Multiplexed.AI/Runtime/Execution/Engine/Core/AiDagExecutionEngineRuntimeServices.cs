using Multiplexed.AI.Runtime.Execution.Engine.Batch;
using Multiplexed.AI.Runtime.Execution.Engine.Creation;
using Multiplexed.AI.Runtime.Execution.Engine.Distributed;
using Multiplexed.AI.Runtime.Execution.Engine.Finalization;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Execution.Engine.Local;
using Multiplexed.AI.Runtime.Execution.Engine.Retention;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;

namespace Multiplexed.AI.Runtime.Execution.Engine.Core
{
    /// <summary>
    /// Provides the composed runtime services required by <see cref="AiDagExecutionEngine"/>.
    /// </summary>
    public sealed class AiDagExecutionEngineRuntimeServices : IAiDagExecutionEngineRuntimeServices
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionEngineRuntimeServices"/> class.
        /// </summary>
        /// <param name="creator">The DAG execution creator.</param>
        /// <param name="localRunner">The local DAG execution runner.</param>
        /// <param name="distributedRunner">The distributed DAG execution runner.</param>
        /// <param name="batchRunner">The distributed DAG batch execution runner.</param>
        /// <param name="claimService">The distributed DAG step claim service.</param>
        /// <param name="claimedStepExecutor">The claimed DAG step executor.</param>
        /// <param name="retentionCoordinator">The DAG retention coordinator.</param>
        /// <param name="finalizationService">The DAG execution finalization service.</param>
        /// <param name="lifecycleHelper">The DAG execution lifecycle helper.</param>
        public AiDagExecutionEngineRuntimeServices(
            AiDagExecutionCreator creator,
            AiDagLocalExecutionRunner localRunner,
            AiDagDistributedExecutionRunner distributedRunner,
            AiDagBatchExecutionRunner batchRunner,
            AiDagStepClaimService claimService,
            AiDagClaimedStepExecutor claimedStepExecutor,
            AiDagRetentionCoordinator retentionCoordinator,
            AiDagExecutionFinalizationService finalizationService,
            AiDagExecutionLifecycleHelper lifecycleHelper)
        {
            Creator = creator ?? throw new ArgumentNullException(nameof(creator));
            LocalRunner = localRunner ?? throw new ArgumentNullException(nameof(localRunner));
            DistributedRunner = distributedRunner ?? throw new ArgumentNullException(nameof(distributedRunner));
            BatchRunner = batchRunner ?? throw new ArgumentNullException(nameof(batchRunner));
            ClaimService = claimService ?? throw new ArgumentNullException(nameof(claimService));
            ClaimedStepExecutor = claimedStepExecutor ?? throw new ArgumentNullException(nameof(claimedStepExecutor));
            RetentionCoordinator = retentionCoordinator ?? throw new ArgumentNullException(nameof(retentionCoordinator));
            FinalizationService = finalizationService ?? throw new ArgumentNullException(nameof(finalizationService));
            LifecycleHelper = lifecycleHelper ?? throw new ArgumentNullException(nameof(lifecycleHelper));
        }

        /// <summary>
        /// Gets the DAG execution creator.
        /// </summary>
        public AiDagExecutionCreator Creator { get; }

        /// <summary>
        /// Gets the local DAG execution runner.
        /// </summary>
        public AiDagLocalExecutionRunner LocalRunner { get; }

        /// <summary>
        /// Gets the distributed DAG execution runner.
        /// </summary>
        public AiDagDistributedExecutionRunner DistributedRunner { get; }

        /// <summary>
        /// Gets the distributed DAG batch execution runner.
        /// </summary>
        public AiDagBatchExecutionRunner BatchRunner { get; }

        /// <summary>
        /// Gets the distributed DAG step claim service.
        /// </summary>
        public AiDagStepClaimService ClaimService { get; }

        /// <summary>
        /// Gets the claimed DAG step executor.
        /// </summary>
        public AiDagClaimedStepExecutor ClaimedStepExecutor { get; }

        /// <summary>
        /// Gets the DAG retention coordinator.
        /// </summary>
        public AiDagRetentionCoordinator RetentionCoordinator { get; }

        /// <summary>
        /// Gets the DAG execution finalization service.
        /// </summary>
        public AiDagExecutionFinalizationService FinalizationService { get; }

        /// <summary>
        /// Gets the DAG execution lifecycle helper.
        /// </summary>
        public AiDagExecutionLifecycleHelper LifecycleHelper { get; }
    }
}