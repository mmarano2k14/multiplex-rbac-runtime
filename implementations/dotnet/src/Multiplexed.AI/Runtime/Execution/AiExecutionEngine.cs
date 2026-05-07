using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Base class for AI execution engines.
    ///
    /// Responsibilities:
    /// - provide shared runtime dependencies
    /// - load persisted execution record and state
    /// - load the live RBAC execution context
    /// - create the global AI execution context
    ///
    /// Derived classes are responsible for:
    /// - execution mode validation
    /// - step scheduling
    /// - step execution strategy
    /// - context rotation policy
    /// - execution progression semantics
    /// </summary>
    public abstract class AiExecutionEngine : IAiExecutionEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionEngine"/> class.
        /// </summary>
        protected AiExecutionEngine(
            IAiExecutionStore store,
            IContextStore contextStore,
            IExecutionContextAccessor accessor,
            IExecutionContextFactory contextFactory,
            IServiceProvider services,
            IAiSequentialPipelineExecutor pipelineExecutor,
            IAiRuntimeLogger logger,
            IAiExecutionStateReader stateReader,
            IAiExecutionStateWriter stateWriter)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(contextStore);
            ArgumentNullException.ThrowIfNull(accessor);
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pipelineExecutor);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(stateReader);
            ArgumentNullException.ThrowIfNull(stateWriter);

            Store = store;
            ContextStore = contextStore;
            Accessor = accessor;
            ContextFactory = contextFactory;
            Services = services;
            PipelineExecutor = pipelineExecutor;
            Logger = logger;
            StateReader = stateReader;
            StateWriter = stateWriter;
        }

        /// <summary>
        /// Gets the durable AI execution store.
        /// </summary>
        protected IAiExecutionStore Store { get; }

        /// <summary>
        /// Gets the RBAC execution context store.
        /// </summary>
        protected IContextStore ContextStore { get; }

        /// <summary>
        /// Gets the live RBAC execution context accessor.
        /// </summary>
        protected IExecutionContextAccessor Accessor { get; }

        /// <summary>
        /// Gets the RBAC execution context factory.
        /// </summary>
        protected IExecutionContextFactory ContextFactory { get; }

        /// <summary>
        /// Gets the root runtime service provider.
        /// </summary>
        protected IServiceProvider Services { get; }

        /// <summary>
        /// Gets the pipeline executor.
        /// </summary>
        protected IAiSequentialPipelineExecutor PipelineExecutor { get; }

        /// <summary>
        /// Gets the centralized runtime logger.
        /// </summary>
        protected IAiRuntimeLogger Logger { get; }

        /// <summary>
        /// Gets the payload-aware execution state reader.
        /// </summary>
        protected IAiExecutionStateReader StateReader { get; }

        /// <summary>
        /// Gets the execution state writer.
        /// </summary>
        protected IAiExecutionStateWriter StateWriter { get; }

        /// <summary>
        /// Creates a new execution without an explicit pipeline name.
        /// This overload is intentionally unsupported.
        /// </summary>
        public virtual Task<AiExecutionRecord> CreateAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "CreateAsync(input) is no longer supported without an explicit pipeline name.");
        }

        /// <summary>
        /// Creates a new execution for the specified pipeline.
        /// Must be implemented by the derived engine.
        /// </summary>
        public abstract Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new execution for the specified pipeline.
        /// Must be implemented by the derived engine.
        /// </summary>
        public abstract Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            IDictionary<string, object?> input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the next unit of work for the specified execution.
        /// Must be implemented by the derived engine.
        /// </summary>
        public abstract Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes one or more ready units of work for the specified execution.
        /// Must be implemented by the derived engine.
        /// </summary>
        /// <param name="executionId">
        /// The unique execution identifier.
        /// </param>
        /// <param name="maxSteps">
        /// The maximum number of ready steps to execute.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The updated execution record after the batch execution attempt.
        /// </returns>
        public abstract Task<AiExecutionRecord> ExecuteBatchAsync(
            string executionId,
            int maxSteps,
            CancellationToken cancellationToken = default);


        /// <summary>
        /// Executes the remaining work until a terminal state is reached.
        /// Must be implemented by the derived engine.
        /// </summary>
        public abstract Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the supplied execution identifier.
        /// </summary>
        protected static void ValidateExecutionId(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));
            }
        }

        /// <summary>
        /// Loads the persisted execution record and mutable execution state.
        /// </summary>
        protected async Task<(AiExecutionRecord Record, AiExecutionState State)> LoadExecutionAsync(
            string executionId,
            CancellationToken cancellationToken)
        {
            var record = await Store.GetRecordAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException("Execution not found.");

            var state = await Store.GetStateAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException("Execution state not found.");

            return (record, state);
        }

        /// <summary>
        /// Loads the live RBAC execution context associated with the supplied context key.
        /// </summary>
        protected async Task<ExecutionContext> LoadContextAsync(string contextKey)
        {
            var context = await ContextStore.GetAsync(contextKey);

            if (context is null)
            {
                throw new InvalidOperationException("RBAC execution context not found.");
            }

            return context;
        }

        /// <summary>
        /// Builds the global AI execution context used during orchestration.
        /// </summary>
        protected AiExecutionContext BuildExecutionContext(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            return new AiExecutionContext(
                record,
                state,
                Services,
                StateReader,
                StateWriter,
                cancellationToken);
        }

        /// <summary>
        /// Ensures the execution defines a pipeline name.
        /// </summary>
        protected static void EnsurePipelineName(AiExecutionRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.PipelineName))
            {
                throw new InvalidOperationException(
                    $"Execution '{record.ExecutionId}' does not define a pipeline name.");
            }
        }

        /// <summary>
        /// Persists an updated execution record and state using optimistic concurrency.
        /// </summary>
        protected async Task PersistAsync(
            AiExecutionRecord record,
            string expectedStepKey,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            var updated = await Store.TryUpdateAsync(
                record.ExecutionId,
                expectedStepKey,
                record,
                state,
                cancellationToken);

            if (!updated)
            {
                throw new InvalidOperationException("Concurrency conflict on execution update.");
            }
        }
    }
}