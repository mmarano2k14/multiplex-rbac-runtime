using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Helpers;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Execution.Engine.Creation
{
    /// <summary>
    /// Creates DAG execution records and initializes their execution state.
    /// </summary>
    public sealed class AiDagExecutionCreator
    {
        private readonly IAiDagExecutionEngineServices _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionCreator"/> class.
        /// </summary>
        /// <param name="services">The DAG execution engine services.</param>
        public AiDagExecutionCreator(
            IAiDagExecutionEngineServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Creates a new DAG execution using a string input payload.
        /// </summary>
        /// <param name="pipelineName">The pipeline name to execute.</param>
        /// <param name="input">The input payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created execution record.</returns>
        public Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));
            }

            return CreateInternalAsync(
                pipelineName,
                state => _services.StateWriter.SetData(state, AiExecutionKeys.Input, input),
                cancellationToken);
        }

        /// <summary>
        /// Creates a new DAG execution using structured state input.
        /// </summary>
        /// <param name="pipelineName">The pipeline name to execute.</param>
        /// <param name="input">The structured input values to seed into execution state.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created execution record.</returns>
        public Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            IDictionary<string, object?> input,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentNullException.ThrowIfNull(input);

            return CreateInternalAsync(
                pipelineName,
                state =>
                {
                    foreach (var pair in input)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Key))
                        {
                            throw new ArgumentException(
                                "Structured input contains an empty or whitespace key.",
                                nameof(input));
                        }

                        _services.StateWriter.SetData(state, pair.Key, pair.Value);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Creates the execution record, seeds the execution state, initializes DAG step state,
        /// creates the AI-owned RBAC context, and persists the execution.
        /// </summary>
        /// <param name="pipelineName">The pipeline name to execute.</param>
        /// <param name="seedState">The callback used to seed initial execution state values.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created execution record.</returns>
        private async Task<AiExecutionRecord> CreateInternalAsync(
            string pipelineName,
            Action<AiExecutionState> seedState,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentNullException.ThrowIfNull(seedState);

            var current = _services.Accessor.Current
                ?? throw new InvalidOperationException("No active RBAC context is available.");

            var preparedPipeline = await _services.PipelineExecutor.PrepareAsync(
                pipelineName,
                cancellationToken);

            if (preparedPipeline.ExecutionMode != AiExecutionMode.Dag)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' is configured for mode '{preparedPipeline.ExecutionMode}' and cannot be created by the DAG engine.");
            }

            if (preparedPipeline.Steps.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' does not contain any resolved steps.");
            }

            var newContextKey = Guid.NewGuid().ToString("N");
            var aiOwnedContext = _services.ContextFactory.CreateCopy(current, newContextKey);

            newContextKey = await _services.ContextStore.SeedAsync(aiOwnedContext);

            var record = new AiExecutionRecord
            {
                PipelineName = pipelineName,
                ExecutionMode = preparedPipeline.ExecutionMode,
                ContextKey = newContextKey,
                Status = AiExecutionStatus.Pending,
                ExecutionContextSnapshot = _services.ContextFactory.CreateSnapshot(current),
                Steps = preparedPipeline.Steps.Select(x => x.Name).ToList(),
                CurrentStep = string.Empty,
                CurrentStepIndex = 0
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId,
                PipelineName = pipelineName,
                PipelineConfig = new Dictionary<string, object?>(
                    preparedPipeline.Config,
                    StringComparer.Ordinal)
            };

            seedState(state);

            var executionContext = new AiExecutionContext(
                record,
                state,
                _services.Services,
                _services.StateReader,
                _services.StateWriter,
                cancellationToken);

            foreach (var step in preparedPipeline.Steps)
            {
                _services.StateWriter.EnsureStepInitialized(state, step);

                var stepState = _services.StateWriter.GetOrCreateStep(state, step.Name);
                stepState.DependsOn = step.DependsOn?.ToList() ?? new List<string>();

                var stepContext = new AiStepExecutionContext(
                    executionContext,
                    step);

                var retryDefinition = await _services.ObservabilityService.Tracer.TraceStepAsync(
                    new AiStepTraceContext
                    {
                        ExecutionId = record.ExecutionId,
                        StepId = step.Name,
                        StepType = "retry.definition",
                        Status = "resolving.policy",
                        WorkerId = _services.RuntimeInstanceIdentity.RuntimeInstanceId
                    },
                    async () =>
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        try
                        {
                            var definition = await _services.PolicyEngineFactory
                                .Create<IAiRetryEngine>(AiPolicyKind.Retry, stepContext)
                                .ResolveRetryDefinitionAsync(cancellationToken);

                            sw.Stop();

                            _services.ObservabilityService.Metrics.Policy.RecordExecution(
                                record.ExecutionId,
                                "retry.definition",
                                success: true,
                                duration: sw.Elapsed);

                            _services.ObservabilityService.Metrics.Policy.RecordDecision(
                                record.ExecutionId,
                                "retry.definition",
                                definition is null
                                    ? AiPolicyResultKind.Block
                                    : AiPolicyResultKind.Success);

                            return definition;
                        }
                        catch
                        {
                            sw.Stop();

                            _services.ObservabilityService.Metrics.Policy.RecordExecution(
                                record.ExecutionId,
                                "retry.definition",
                                success: false,
                                duration: sw.Elapsed);

                            _services.ObservabilityService.Metrics.Policy.RecordFailure(
                                record.ExecutionId,
                                "retry.definition");

                            throw;
                        }
                    });

                stepState.Retry = retryDefinition;
                stepState.RetryState ??= new AiStepRetryState();
            }

            if (_services.DagStore is not null)
            {
                await _services.DagStore
                    .CreateAsync(
                        record,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _services.Store
                    .CreateAsync(
                        record,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var pipelineKey = $"{preparedPipeline.Name}:{preparedPipeline.Version}";
            var runtimeInstanceId = _services.RuntimeInstanceIdentity.RuntimeInstanceId;

            await AiDagExecutionHelpers.RecordDagLedgerEventAsync(
                    _services,
                    record.ExecutionId,
                    pipelineKey,
                    "_execution",
                    "_execution",
                    runtimeInstanceId,
                    claimToken: null,
                    concurrencyContext: null,
                    AiDecisionLedgerCategory.Execution,
                    AiDecisionLedgerEvents.Execution.Created,
                    AiDecisionLedgerOutcome.Persisted,
                    "DAG execution created and persisted.",
                    new Dictionary<string, string>
                    {
                        ["pipeline.name"] = record.PipelineName,
                        ["pipeline.version"] = preparedPipeline.Version,
                        ["execution.mode"] = record.ExecutionMode.ToString(),
                        ["step.count"] = preparedPipeline.Steps.Count.ToString(),
                        ["context.key"] = record.ContextKey
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            _services.Logger.Engine.ExecutionCreated(record);

            _services.ObservabilityService.Metrics.Execution.RecordExecutionStarted(
                record.ExecutionId);

            _services.Logger.Engine.LogInformation(
                $"[AI DAG] Execution created. ExecutionId='{record.ExecutionId}', Pipeline='{record.PipelineName}', Mode='{record.ExecutionMode}', StepCount='{preparedPipeline.Steps.Count}', ContextKey='{record.ContextKey}'.");

            return record;
        }
    }
}