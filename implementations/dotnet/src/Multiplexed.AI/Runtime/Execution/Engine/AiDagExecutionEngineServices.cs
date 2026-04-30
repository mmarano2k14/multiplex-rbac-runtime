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
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution.Engine
{
    /// <summary>
    /// Default implementation of <see cref="IAiDagExecutionEngineServices"/>.
    ///
    /// PURPOSE:
    /// - Provides a concrete dependency bundle for <see cref="AiDagExecutionEngine"/>.
    /// - Centralizes dependency injection for the engine.
    /// - Reduces constructor complexity and churn.
    ///
    /// IMPORTANT:
    /// - This class must remain a pure container.
    /// - It must not contain business logic.
    /// - It must not resolve services dynamically.
    /// </summary>
    public sealed class AiDagExecutionEngineServices : IAiDagExecutionEngineServices
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionEngineServices"/> class.
        /// </summary>
        public AiDagExecutionEngineServices(
            IAiExecutionStore store,
            IContextStore contextStore,
            IExecutionContextAccessor accessor,
            IExecutionContextFactory contextFactory,
            IServiceProvider services,
            IAiSequentialPipelineExecutor pipelineExecutor,
            IAiRuntimeLogger logger,
            IAiExecutionCleanupService cleanupService,
            IOptions<AiEngineOptions> aiOptions,
            IAiRuntimeObservability observabilityService,
            IAiStepResultPayloadCompactor payloadCompactor,
            IAiExecutionStateReader stateReader,
            IAiExecutionStateWriter stateWriter,
            IAiExecutionStepResolver stepResolver,
            IAiExecutionRetentionService retentionService,
            IAiDagExecutionStore? dagStore = null,
            IAiExecutionSnapshotService<ExecutionContextSnapshot>? snapshotService = null)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            ContextStore = contextStore ?? throw new ArgumentNullException(nameof(contextStore));
            Accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            ContextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            PipelineExecutor = pipelineExecutor ?? throw new ArgumentNullException(nameof(pipelineExecutor));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
            AiOptions = aiOptions ?? throw new ArgumentNullException(nameof(aiOptions));
            //Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            PayloadCompactor = payloadCompactor ?? throw new ArgumentNullException(nameof(payloadCompactor));
            StateReader = stateReader ?? throw new ArgumentNullException(nameof(stateReader));
            StateWriter = stateWriter ?? throw new ArgumentNullException(nameof(stateWriter));
            StepResolver = stepResolver ?? throw new ArgumentNullException(nameof(stepResolver));
            RetentionService = retentionService ?? throw new ArgumentNullException(nameof(retentionService));
            ObservabilityService = observabilityService ?? throw new ArgumentNullException(nameof(observabilityService));

            DagStore = dagStore;
            SnapshotService = snapshotService;
        }

        /// <inheritdoc />
        public IAiExecutionStore Store { get; }

        /// <inheritdoc />
        public IContextStore ContextStore { get; }

        /// <inheritdoc />
        public IExecutionContextAccessor Accessor { get; }

        /// <inheritdoc />
        public IExecutionContextFactory ContextFactory { get; }

        /// <inheritdoc />
        public IServiceProvider Services { get; }

        /// <inheritdoc />
        public IAiSequentialPipelineExecutor PipelineExecutor { get; }

        /// <inheritdoc />
        public IAiRuntimeLogger Logger { get; }

        /// <inheritdoc />
        public IAiExecutionCleanupService CleanupService { get; }

        /// <inheritdoc />
        public IOptions<AiEngineOptions> AiOptions { get; }

        /// <inheritdoc />
        //public IAiRuntimeMetrics Metrics { get; }

        /// <inheritdoc />
        public IAiStepResultPayloadCompactor PayloadCompactor { get; }

        /// <inheritdoc />
        public IAiExecutionStateReader StateReader { get; }

        /// <inheritdoc />
        public IAiExecutionStateWriter StateWriter { get; }

        /// <inheritdoc />
        public IAiExecutionStepResolver StepResolver { get; }

        /// <inheritdoc />
        public IAiDagExecutionStore? DagStore { get; }

        /// <inheritdoc />
        public IAiExecutionSnapshotService<ExecutionContextSnapshot>? SnapshotService { get; }

        /// <inheritdoc />
        public IAiExecutionRetentionService RetentionService { get; }

        /// <inheritdoc />
        public IAiRuntimeObservability ObservabilityService { get; }

    }
}