using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Plugins;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Strongly typed RAG retrieval step.
    ///
    /// DESIGN:
    /// - The runtime execution context is the TExecutionContext
    /// - The step remains infrastructure only
    /// - Business retrieval logic stays in external IRagOperation implementations
    /// - When the execution context is an <see cref="AiExecutionContext"/>,
    ///   the persisted RBAC execution snapshot is propagated to the plugin context
    /// </summary>
    /// <typeparam name="TExecutionContext">
    /// The strongly typed execution context used by this step.
    /// </typeparam>
    public sealed class RagRetrievalStep<TExecutionContext>
    {
        private readonly IRagOperationResolver _operationResolver;
        private readonly IRagStepResultNormalizer _stepResultNormalizer;
        private readonly IAiRuntimeLogger _logger;

        public RagRetrievalStep(
            IRagOperationResolver operationResolver,
            IRagStepResultNormalizer stepResultNormalizer,
            IAiRuntimeLogger logger)
        {
            _operationResolver = operationResolver ?? throw new ArgumentNullException(nameof(operationResolver));
            _stepResultNormalizer = stepResultNormalizer ?? throw new ArgumentNullException(nameof(stepResultNormalizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes a RAG retrieval operation using a strongly typed execution context.
        /// </summary>
        public async Task<RagRetrievalBatch> ExecuteAsync(
            TExecutionContext executionContext,
            IReadOnlyDictionary<string, object?> inputs,
            RagRetrievalStepConfig config,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(executionContext);
            ArgumentNullException.ThrowIfNull(inputs);
            ArgumentNullException.ThrowIfNull(config);

            if (string.IsNullOrWhiteSpace(config.Operation))
            {
                throw new ArgumentException("RAG retrieval operation cannot be null or whitespace.", nameof(config));
            }

            _logger.Engine.LogInformation($"Resolving RAG operation '{config.Operation}'.");

            var operation = _operationResolver.Resolve(config.Operation);

            if (operation.ExecutionContextType != typeof(TExecutionContext))
            {
                throw new InvalidOperationException(
                    $"RAG operation '{operation.Key}' expects execution context type '{operation.ExecutionContextType.FullName}', " +
                    $"but step was instantiated with '{typeof(TExecutionContext).FullName}'.");
            }

            if (operation is not IRagOperation<TExecutionContext> typedOperation)
            {
                throw new InvalidOperationException(
                    $"RAG operation '{operation.Key}' does not implement IRagOperation<{typeof(TExecutionContext).Name}>.");
            }

            var executionContextSnapshot = executionContext is AiExecutionContext aiExecutionContext
                ? aiExecutionContext.Record.ExecutionContextSnapshot
                : null;

            var pluginContext = new PluginExecutionContext<TExecutionContext>(
                executionContext,
                executionContextSnapshot,
                inputs);

            _logger.Engine.LogInformation($"Executing RAG operation '{operation.Key}'.");

            RagRetrievalBatch result;

            try
            {
                result = await typedOperation.ExecuteAsync(pluginContext, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Engine.LogError(
                    $"RAG operation '{operation.Key}' failed: {ex.GetType().Name}: {ex.Message}");
                throw;
            }

            if (result == null)
            {
                throw new InvalidOperationException(
                    $"RAG operation '{operation.Key}' returned null '{nameof(RagRetrievalBatch)}'.");
            }

            var normalized = _stepResultNormalizer.Normalize(result);

            if (normalized is not RagRetrievalBatch normalizedBatch)
            {
                throw new InvalidOperationException(
                    $"RAG operation '{operation.Key}' returned a normalized result of type " +
                    $"'{normalized?.GetType().FullName ?? "null"}' instead of '{typeof(RagRetrievalBatch).FullName}'.");
            }

            _logger.Engine.LogInformation(
                $"RAG operation '{operation.Key}' completed successfully with " +
                $"{normalizedBatch.Items?.Count ?? 0} normalized item(s).");

            return normalizedBatch;
        }
    }
}