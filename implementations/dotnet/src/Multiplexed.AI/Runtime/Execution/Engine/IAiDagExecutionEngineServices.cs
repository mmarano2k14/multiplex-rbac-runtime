using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Cleanup;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution.Engine
{
    /// <summary>
    /// Defines a strongly-typed dependency bundle for <see cref="AiDagExecutionEngine"/>.
    ///
    /// PURPOSE:
    /// - Groups all required engine dependencies into a single injectable contract.
    /// - Stabilizes the engine constructor to avoid frequent refactoring.
    /// - Improves testability by allowing a single object to be mocked or constructed.
    ///
    /// IMPORTANT:
    /// - This interface must not introduce logic.
    /// - It must not resolve services dynamically.
    /// - It should only expose dependencies required by the engine.
    /// </summary>
    public interface IAiDagExecutionEngineServices
    {
        /// <summary>
        /// Gets the execution store used to persist execution state.
        /// </summary>
        IAiExecutionStore Store { get; }

        /// <summary>
        /// Gets the context store used for execution context persistence.
        /// </summary>
        IContextStore ContextStore { get; }

        /// <summary>
        /// Gets the execution context accessor.
        /// </summary>
        IExecutionContextAccessor Accessor { get; }

        /// <summary>
        /// Gets the execution context factory.
        /// </summary>
        IExecutionContextFactory ContextFactory { get; }

        /// <summary>
        /// Gets the root service provider.
        /// </summary>
        IServiceProvider Services { get; }

        /// <summary>
        /// Gets the sequential pipeline executor.
        /// </summary>
        IAiSequentialPipelineExecutor PipelineExecutor { get; }

        /// <summary>
        /// Gets the runtime logger.
        /// </summary>
        IAiRuntimeLogger Logger { get; }

        /// <summary>
        /// Gets the execution cleanup service.
        /// </summary>
        IAiExecutionCleanupService CleanupService { get; }

        /// <summary>
        /// Gets the AI engine options.
        /// </summary>
        IOptions<AiEngineOptions> AiOptions { get; }

        /// <summary>
        /// Gets runtime metrics collector.
        /// </summary>
        //IAiRuntimeMetrics Metrics { get; }

        /// <summary>
        /// Gets the step result payload compactor.
        /// </summary>
        IAiStepResultPayloadCompactor PayloadCompactor { get; }

        /// <summary>
        /// Gets the execution state reader.
        /// </summary>
        IAiExecutionStateReader StateReader { get; }

        /// <summary>
        /// Gets the execution state writer.
        /// </summary>
        IAiExecutionStateWriter StateWriter { get; }

        /// <summary>
        /// Gets the step resolver used to resolve hot and archived step states.
        ///
        /// PURPOSE:
        /// - Allows the DAG engine to read steps from hot state first.
        /// - Falls back to the archived step index and payload store when steps were evicted.
        /// - Keeps selector and convergence correct after retention compaction/eviction.
        ///
        /// IMPORTANT:
        /// - This is required once Evict or Hybrid retention modes are enabled.
        /// - The resolver may use local scoped cache for performance.
        /// </summary>
        IAiExecutionStepResolver StepResolver { get; }

        /// <summary>
        /// Gets the DAG execution store.
        /// </summary>
        IAiDagExecutionStore? DagStore { get; }

        /// <summary>
        /// Gets the execution snapshot service.
        /// </summary>
        IAiExecutionSnapshotService<ExecutionContextSnapshot>? SnapshotService { get; }

        /// <summary>
        /// Gets the execution retention service used to apply step compaction and eviction.
        /// </summary>
        IAiExecutionRetentionService RetentionService { get; }

        /// <summary>
        /// Gets the execution Runtime Observability service used to emit structured events about execution lifecycle and behavior.
        /// </summary>
        IAiRuntimeObservability ObservabilityService { get; }

        /// <summary>
        /// Gets the factory used to create step-scoped AI policy engine instances.
        /// </summary>
        /// <remarks>
        /// This factory is responsible for instantiating the appropriate policy engine
        /// (for example, retry, retention, eviction) based on the specified policy kind.
        ///
        /// DESIGN:
        /// - Engines are created per step execution to ensure isolation and multi-worker safety.
        /// - No engine instance is shared across executions.
        /// - The factory resolves the correct engine implementation via the policy engine registry.
        ///
        /// USAGE:
        /// - The DAG or runtime layer uses this factory to obtain a policy engine
        ///   and execute domain-specific behavior (e.g., retry handling).
        ///
        /// EXAMPLE:
        /// <code>
        /// var engine = PolicyFactory.Create&lt;IAiRetryEngine&gt;(AiPolicyKind.Retry, stepContext);
        /// await engine.HandleFailureAsync(...);
        /// </code>
        /// </remarks>
        IAiPolicyEngineFactory PolicyEngineFactory { get; }
    }
}