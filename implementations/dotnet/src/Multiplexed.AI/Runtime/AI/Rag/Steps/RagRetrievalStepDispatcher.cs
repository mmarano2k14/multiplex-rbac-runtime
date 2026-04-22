using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// High-performance dispatcher bridging the runtime AI execution context
    /// and strongly typed RAG retrieval steps.
    ///
    /// CORE RESPONSIBILITY:
    /// - Resolve operation
    /// - Determine TExecutionContext
    /// - Invoke RagRetrievalStep&lt;TExecutionContext&gt; dynamically
    ///
    /// PERFORMANCE:
    /// - Uses compiled delegate caching
    /// - Avoids reflection on hot path
    ///
    /// THREAD SAFE:
    /// - Fully safe for multi-worker execution
    ///
    /// IMPORTANT:
    /// - The public runtime input is always <see cref="AiExecutionContext"/>
    /// - Strong typing is applied internally against the operation contract
    /// - The dispatcher forwards both operation metadata and provider resolution
    ///   dependencies to the typed retrieval step
    /// </summary>
    public sealed class RagRetrievalStepDispatcher : IRagRetrievalStepDispatcher
    {
        private readonly IRagOperationResolver _operationResolver;
        private readonly IRagOperationRegistry _operationRegistry;
        private readonly INormalizingRagProviderResolver _providerResolver;
        private readonly IRagStepResultNormalizer _normalizer;
        private readonly IAiRuntimeLogger _logger;

        private readonly ConcurrentDictionary<Type, Func<AiExecutionContext, IReadOnlyDictionary<string, object?>, RagRetrievalStepConfig, CancellationToken, Task<RagRetrievalBatch>>> _cache
            = new();

        public RagRetrievalStepDispatcher(
            IRagOperationResolver operationResolver,
            IRagOperationRegistry operationRegistry,
            INormalizingRagProviderResolver providerResolver,
            IRagStepResultNormalizer normalizer,
            IAiRuntimeLogger logger)
        {
            _operationResolver = operationResolver ?? throw new ArgumentNullException(nameof(operationResolver));
            _operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
            _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<RagRetrievalBatch> ExecuteAsync(
            AiExecutionContext executionContext,
            IReadOnlyDictionary<string, object?> inputs,
            RagRetrievalStepConfig config,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(executionContext);
            ArgumentNullException.ThrowIfNull(inputs);
            ArgumentNullException.ThrowIfNull(config);

            if (string.IsNullOrWhiteSpace(config.Operation))
            {
                throw new ArgumentException("Operation cannot be null or whitespace.", nameof(config));
            }

            var operation = _operationResolver.Resolve(config.Operation);
            var contextType = operation.ExecutionContextType;

            var executor = _cache.GetOrAdd(contextType, CreateExecutor);

            return executor(executionContext, inputs, config, cancellationToken);
        }

        /// <summary>
        /// Creates and caches a compiled delegate for a given TExecutionContext.
        /// </summary>
        private Func<AiExecutionContext, IReadOnlyDictionary<string, object?>, RagRetrievalStepConfig, CancellationToken, Task<RagRetrievalBatch>> CreateExecutor(
            Type contextType)
        {
            var method = typeof(RagRetrievalStepDispatcher)
                .GetMethod(nameof(ExecuteInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(contextType);

            return (Func<AiExecutionContext, IReadOnlyDictionary<string, object?>, RagRetrievalStepConfig, CancellationToken, Task<RagRetrievalBatch>>)
                Delegate.CreateDelegate(
                    typeof(Func<AiExecutionContext, IReadOnlyDictionary<string, object?>, RagRetrievalStepConfig, CancellationToken, Task<RagRetrievalBatch>>),
                    this,
                    method);
        }

        /// <summary>
        /// Strongly typed execution path (compiled and cached).
        /// </summary>
        private async Task<RagRetrievalBatch> ExecuteInternal<TExecutionContext>(
            AiExecutionContext executionContext,
            IReadOnlyDictionary<string, object?> inputs,
            RagRetrievalStepConfig config,
            CancellationToken cancellationToken)
        {
            if (executionContext is not TExecutionContext typedContext)
            {
                throw new InvalidOperationException(
                    $"Expected execution context type '{typeof(TExecutionContext).FullName}', " +
                    $"but received '{executionContext.GetType().FullName}'.");
            }

            var step = new RagRetrievalStep<TExecutionContext>(
                _operationResolver,
                _operationRegistry,
                _providerResolver,
                _normalizer,
                _logger);

            return await step.ExecuteAsync(
                typedContext,
                inputs,
                config,
                cancellationToken).ConfigureAwait(false);
        }
    }
}