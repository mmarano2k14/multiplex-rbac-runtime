using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Tracing;
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
    /// Executes AI pipelines using DAG-based orchestration.
    /// </summary>
    public sealed class AiDagExecutionEngine : AiExecutionEngine
    {
        private readonly IAiDagExecutionEngineServices _engineServices;

        private readonly AiDagExecutionCreator _creator;
        private readonly AiDagLocalExecutionRunner _localRunner;
        private readonly AiDagDistributedExecutionRunner _distributedRunner;
        private readonly AiDagBatchExecutionRunner _batchRunner;

        private readonly AiDagStepClaimService _claimService;
        private readonly AiDagClaimedStepExecutor _claimedStepExecutor;
        private readonly AiDagRetentionCoordinator _retentionCoordinator;
        private readonly AiDagExecutionFinalizationService _finalizationService;
        private readonly AiDagExecutionLifecycleHelper _lifecycleHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionEngine"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services.
        /// </param>
        public AiDagExecutionEngine(
            IAiDagExecutionEngineServices engineServices)
            : base(
                engineServices.Store,
                engineServices.ContextStore,
                engineServices.Accessor,
                engineServices.ContextFactory,
                engineServices.Services,
                engineServices.PipelineExecutor,
                engineServices.Logger,
                engineServices.StateReader,
                engineServices.StateWriter)
        {
            _engineServices = engineServices
                ?? throw new ArgumentNullException(nameof(engineServices));

            _lifecycleHelper = new AiDagExecutionLifecycleHelper(
                engineServices);

            _retentionCoordinator = new AiDagRetentionCoordinator(
                engineServices);

            _claimService = new AiDagStepClaimService(
                engineServices);

            _claimedStepExecutor = new AiDagClaimedStepExecutor(
                engineServices);

            _finalizationService = new AiDagExecutionFinalizationService(
                engineServices,
                _retentionCoordinator);

            _creator = new AiDagExecutionCreator(
                engineServices);

            _localRunner = new AiDagLocalExecutionRunner(
                engineServices,
                _lifecycleHelper);

            _distributedRunner = new AiDagDistributedExecutionRunner(
                engineServices,
                _claimService,
                _claimedStepExecutor,
                _retentionCoordinator,
                _finalizationService,
                _lifecycleHelper);

            _batchRunner = new AiDagBatchExecutionRunner(
                engineServices,
                _claimService,
                _claimedStepExecutor,
                _finalizationService,
                _lifecycleHelper);
        }

        /// <inheritdoc />
        public override Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default)
        {
            return _creator.CreateAsync(
                pipelineName,
                input,
                cancellationToken);
        }

        /// <inheritdoc />
        public override Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            IDictionary<string, object?> input,
            CancellationToken cancellationToken = default)
        {
            return _creator.CreateAsync(
                pipelineName,
                input,
                cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (_engineServices.DagStore is not null)
            {
                return await _distributedRunner.ExecuteNextAsync(
                    executionId,
                    async contextKey =>
                    {
                        var rbacContext = await LoadContextAsync(contextKey);
                        Accessor.Set(rbacContext);
                    },
                    BuildExecutionContext,
                    PersistAsync,
                    EnsurePipelineName,
                    ValidateExecutionId,
                    cancellationToken);
            }

            return await _localRunner.ExecuteNextAsync(
                executionId,
                LoadExecutionAsync,
                async contextKey =>
                {
                    var rbacContext = await LoadContextAsync(contextKey);
                    Accessor.Set(rbacContext);
                },
                BuildExecutionContext,
                PersistAsync,
                EnsurePipelineName,
                ValidateExecutionId,
                cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteBatchAsync(
            string executionId,
            int maxSteps,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

            if (_engineServices.DagStore is not null)
            {
                return await _batchRunner.ExecuteBatchAsync(
                    executionId,
                    maxSteps,
                    async contextKey =>
                    {
                        var rbacContext = await LoadContextAsync(contextKey);
                        Accessor.Set(rbacContext);
                    },
                    BuildExecutionContext,
                    PersistAsync,
                    EnsurePipelineName,
                    ValidateExecutionId,
                    cancellationToken);
            }

            return await _localRunner.ExecuteNextAsync(
                executionId,
                LoadExecutionAsync,
                async contextKey =>
                {
                    var rbacContext = await LoadContextAsync(contextKey);
                    Accessor.Set(rbacContext);
                },
                BuildExecutionContext,
                PersistAsync,
                EnsurePipelineName,
                ValidateExecutionId,
                cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await _engineServices.ObservabilityService.Tracer.TraceExecutionAsync(
                new AiExecutionTraceContext
                {
                    ExecutionId = executionId,
                    ExecutionMode = "Dag",
                    Status = "Running",
                    WorkerId = Environment.MachineName
                },
                async () =>
                {
                    AiExecutionRecord record;

                    do
                    {
                        record = await ExecuteNextAsync(
                            executionId,
                            cancellationToken);

                        if (record.Status == AiExecutionStatus.Waiting)
                        {
                            _engineServices.Logger.Engine.LogInformation(
                                $"[AI DAG] ExecuteAll stopped in Waiting. ExecutionId='{record.ExecutionId}', Status='{record.Status}'.");

                            return record;
                        }
                    }
                    while (!record.IsTerminal);

                    _engineServices.Logger.Engine.LogInformation(
                        $"[AI DAG] ExecuteAll reached terminal state. ExecutionId='{record.ExecutionId}', Status='{record.Status}'.");

                    return record;
                });
        }
    }
}