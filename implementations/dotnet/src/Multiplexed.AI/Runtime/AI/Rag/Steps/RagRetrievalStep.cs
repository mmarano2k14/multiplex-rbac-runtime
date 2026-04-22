using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Plugins;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    public sealed class RagRetrievalStep<TExecutionContext>
    {
        private readonly IRagOperationResolver _operationResolver;
        private readonly IRagOperationRegistry _operationRegistry;
        private readonly INormalizingRagProviderResolver _providerResolver;
        private readonly IRagStepResultNormalizer _stepResultNormalizer;
        private readonly IAiRuntimeLogger _logger;

        public RagRetrievalStep(
            IRagOperationResolver operationResolver,
            IRagOperationRegistry operationRegistry,
            INormalizingRagProviderResolver providerResolver,
            IRagStepResultNormalizer stepResultNormalizer,
            IAiRuntimeLogger logger)
        {
            _operationResolver = operationResolver ?? throw new ArgumentNullException(nameof(operationResolver));
            _operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
            _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
            _stepResultNormalizer = stepResultNormalizer ?? throw new ArgumentNullException(nameof(stepResultNormalizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
            var descriptor = _operationRegistry.Get(operation.Key);

            _logger.Engine.LogInformation(
                $"RAG operation '{operation.Key}' uses provider '{descriptor.ProviderKey}' (UseProviderExecution={descriptor.UseProviderExecution}).");

            RagRetrievalBatch result;

            if (descriptor.UseProviderExecution)
            {
                _logger.Engine.LogInformation($"Executing provider mode for '{operation.Key}'.");

                try
                {
                    var provider = _providerResolver.Resolve(descriptor.ProviderKey);

                    var ragContext = new RagExecutionContext
                    {
                        QueryKey = operation.Key,
                        Inputs = inputs,
                        Metadata = new Dictionary<string, object?>
                        {
                            ["operation"] = operation.Key
                        }
                    };

                    result = await provider
                        .RetrieveNormalizedAsync(ragContext, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Engine.LogError(
                        $"Provider execution failed for '{operation.Key}', falling back to operation mode. Error: {ex.Message}");

                    result = await ExecuteOperationFallback(
                        operation,
                        executionContext,
                        inputs,
                        cancellationToken);
                }
            }
            else
            {
                result = await ExecuteOperationFallback(
                    operation,
                    executionContext,
                    inputs,
                    cancellationToken);
            }

            var normalized = _stepResultNormalizer.Normalize(result);

            if (normalized is not RagRetrievalBatch normalizedBatch)
            {
                throw new InvalidOperationException(
                    $"RAG operation '{operation.Key}' returned invalid normalized result.");
            }

            _logger.Engine.LogInformation(
                $"RAG operation '{operation.Key}' completed with {normalizedBatch.Items?.Count ?? 0} item(s).");

            return normalizedBatch;
        }

        private async Task<RagRetrievalBatch> ExecuteOperationFallback(
            IRagOperation operation,
            TExecutionContext executionContext,
            IReadOnlyDictionary<string, object?> inputs,
            CancellationToken cancellationToken)
        {
            if (operation.ExecutionContextType != typeof(TExecutionContext))
            {
                throw new InvalidOperationException(
                    $"RAG operation '{operation.Key}' expects '{operation.ExecutionContextType.Name}' but got '{typeof(TExecutionContext).Name}'.");
            }

            if (operation is not IRagOperation<TExecutionContext> typedOperation)
            {
                throw new InvalidOperationException(
                    $"RAG operation '{operation.Key}' does not implement expected typed interface.");
            }

            var executionContextSnapshot = executionContext is AiExecutionContext aiExecutionContext
                ? aiExecutionContext.Record.ExecutionContextSnapshot
                : null;

            var pluginContext = new PluginExecutionContext<TExecutionContext>(
                executionContext,
                executionContextSnapshot,
                inputs);

            return await typedOperation.ExecuteAsync(pluginContext, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}