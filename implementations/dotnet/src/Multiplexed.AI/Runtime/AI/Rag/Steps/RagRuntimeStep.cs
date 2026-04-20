using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Pipeline step that executes a runtime-oriented RAG provider.
    ///
    /// PURPOSE:
    /// - Retrieves context from runtime state or execution-related data.
    /// - Allows RAG flows to enrich prompt context with engine-level knowledge.
    /// - Returns a serializable retrieval batch for downstream merge or composition.
    /// </summary>
    [AiStep("rag.runtime")]
    public sealed class RagRuntimeStep : IAiStep
    {
        private readonly INormalizingRagProviderResolver _providerResolver;

        public RagRuntimeStep(INormalizingRagProviderResolver providerResolver)
        {
            _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        }

        public string Name => "rag.runtime";

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var providerKey = RagStepHelper.GetRequiredProviderKey(context);
            var provider = _providerResolver.Resolve(providerKey);
            var ragContext = RagStepHelper.BuildRagExecutionContext(context);

            var batch = await provider.RetrieveNormalizedAsync(ragContext, cancellationToken);

            return AiStepResult.Ok(
                output: $"Runtime retrieval completed with {batch.Items.Count} item(s).",
                data: RagStepHelper.BuildRetrievalStepResultData(batch, providerKey));
        }
    }
}