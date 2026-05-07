using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Stores;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Routes execution requests to the appropriate execution engine
    /// based on the persisted <see cref="AiExecutionRecord.ExecutionMode"/>.
    ///
    /// Responsibilities:
    /// - Load the execution record
    /// - Determine the configured execution mode
    /// - Delegate execution to the matching engine
    ///
    /// This router allows sequential and DAG execution engines
    /// to coexist without mixing their orchestration logic.
    /// </summary>
    public sealed class AiExecutionEngineRouter : IAiExecutionEngine
    {
        private readonly IAiExecutionStore _store;
        private readonly AiSequentialExecutionEngine _sequentialEngine;
        private readonly AiDagExecutionEngine _dagEngine;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionEngineRouter"/> class.
        /// </summary>
        /// <param name="store">The execution store used to load persisted execution records.</param>
        /// <param name="sequentialEngine">The engine responsible for sequential execution.</param>
        /// <param name="dagEngine">The engine responsible for DAG-based execution.</param>
        public AiExecutionEngineRouter(
            IAiExecutionStore store,
            AiSequentialExecutionEngine sequentialEngine,
            AiDagExecutionEngine dagEngine)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(sequentialEngine);
            ArgumentNullException.ThrowIfNull(dagEngine);

            _store = store;
            _sequentialEngine = sequentialEngine;
            _dagEngine = dagEngine;
        }

        /// <summary>
        /// Creates a new execution without an explicit pipeline name.
        /// This method is intentionally unsupported.
        /// </summary>
        public Task<AiExecutionRecord> CreateAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "CreateAsync(input) is no longer supported without an explicit pipeline name.");
        }

        /// <summary>
        /// Creates a new execution.
        ///
        /// NOTE:
        /// Creation should normally be routed by pipeline configuration
        /// at a higher level. This router does not infer execution mode
        /// from the pipeline name by itself.
        /// </summary>
        public Task<AiExecutionRecord> CreateAsync(
            string pipelineName,
            string input,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "CreateAsync(pipelineName, input) should be handled by the appropriate engine based on pipeline configuration.");
        }

        /// <summary>
        /// Executes the next unit of work for the specified execution.
        ///
        /// The target engine is selected using the persisted execution mode.
        /// </summary>
        public async Task<AiExecutionRecord> ExecuteNextAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));
            }

            var record = await _store.GetRecordAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException("Execution not found.");

            return record.ExecutionMode switch
            {
                AiExecutionMode.Sequential => await _sequentialEngine.ExecuteNextAsync(
                    executionId,
                    cancellationToken),

                AiExecutionMode.Dag => await _dagEngine.ExecuteNextAsync(
                    executionId,
                    cancellationToken),

                _ => throw new InvalidOperationException(
                    $"Unsupported execution mode '{record.ExecutionMode}'.")
            };
        }

        /// <summary>
        /// Executes one or more ready units of work for the specified execution.
        ///
        /// The target engine is selected using the persisted execution mode.
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
        public async Task<AiExecutionRecord> ExecuteBatchAsync(
            string executionId,
            int maxSteps,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

            var record = await _store.GetRecordAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException("Execution not found.");

            return record.ExecutionMode switch
            {
                AiExecutionMode.Sequential => await _sequentialEngine.ExecuteNextAsync(
                    executionId,
                    cancellationToken),

                AiExecutionMode.Dag => await _dagEngine.ExecuteBatchAsync(
                    executionId,
                    maxSteps,
                    cancellationToken),

                _ => throw new InvalidOperationException(
                    $"Unsupported execution mode '{record.ExecutionMode}'.")
            };
        }

        /// <summary>
        /// Executes the remaining work until a terminal state is reached,
        /// or until the delegated engine stops progressing.
        /// </summary>
        public async Task<AiExecutionRecord> ExecuteAllAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));
            }

            var record = await _store.GetRecordAsync(executionId, cancellationToken)
                ?? throw new InvalidOperationException("Execution not found.");

            return record.ExecutionMode switch
            {
                AiExecutionMode.Sequential => await _sequentialEngine.ExecuteAllAsync(
                    executionId,
                    cancellationToken),

                AiExecutionMode.Dag => await _dagEngine.ExecuteAllAsync(
                    executionId,
                    cancellationToken),

                _ => throw new InvalidOperationException(
                    $"Unsupported execution mode '{record.ExecutionMode}'.")
            };
        }

        public Task<AiExecutionRecord> CreateAsync(string pipelineName, IDictionary<string, object?> input, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "CreateAsync(pipelineName, input) should be handled by the appropriate engine based on pipeline configuration.");
        }
    }
}