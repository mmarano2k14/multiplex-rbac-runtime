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
    /// Provides the composed runtime services required by the DAG execution engine.
    /// </summary>
    ///
    /// <remarks>
    /// PURPOSE:
    /// - Centralize DAG runtime composition.
    /// - Expose the internal execution orchestration services used by the engine.
    /// - Reduce constructor complexity inside <see cref="AiDagExecutionEngine"/>.
    ///
    /// IMPORTANT:
    /// - This interface acts as an internal runtime composition facade.
    /// - It does not contain execution logic itself.
    /// - It only exposes already-composed runtime services.
    /// </remarks>
    public interface IAiDagExecutionEngineRuntimeServices
    {
        /// <summary>
        /// Gets the DAG execution creator.
        /// </summary>
        AiDagExecutionCreator Creator { get; }

        /// <summary>
        /// Gets the local DAG execution runner.
        /// </summary>
        AiDagLocalExecutionRunner LocalRunner { get; }

        /// <summary>
        /// Gets the distributed DAG execution runner.
        /// </summary>
        AiDagDistributedExecutionRunner DistributedRunner { get; }

        /// <summary>
        /// Gets the distributed DAG batch execution runner.
        /// </summary>
        AiDagBatchExecutionRunner BatchRunner { get; }

        /// <summary>
        /// Gets the distributed DAG step claim service.
        /// </summary>
        AiDagStepClaimService ClaimService { get; }

        /// <summary>
        /// Gets the claimed DAG step executor.
        /// </summary>
        AiDagClaimedStepExecutor ClaimedStepExecutor { get; }

        /// <summary>
        /// Gets the DAG retention coordinator.
        /// </summary>
        AiDagRetentionCoordinator RetentionCoordinator { get; }

        /// <summary>
        /// Gets the DAG execution finalization service.
        /// </summary>
        AiDagExecutionFinalizationService FinalizationService { get; }

        /// <summary>
        /// Gets the DAG execution lifecycle helper.
        /// </summary>
        AiDagExecutionLifecycleHelper LifecycleHelper { get; }
    }
}