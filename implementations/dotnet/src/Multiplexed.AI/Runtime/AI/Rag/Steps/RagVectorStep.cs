using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Pipeline step that executes a single vector-oriented RAG provider.
    ///
    /// PURPOSE:
    /// - Acts as the expert DAG entry point for vector retrieval.
    /// - Builds a normalized <see cref="Abstractions.Models.RagExecutionContext"/>.
    /// - Delegates retrieval to a resolved provider.
    ///
    /// DESIGN:
    /// - This step does not know how vector retrieval is implemented.
    /// - Provider resolution is delegated to <see cref="INormalizingRagProviderResolver"/>.
    /// - The step returns a serializable <c>RagRetrievalBatch</c>.
    /// </summary>
    [AiStep("rag.vector")]
    public sealed class RagVectorStep : IAiStep
    {
        private readonly INormalizingRagProviderResolver _providerResolver;

        public RagVectorStep(INormalizingRagProviderResolver providerResolver)
        {
            _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        }

        public string Name => "rag.vector";

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
                output: $"Vector retrieval completed with {batch.Items.Count} item(s).",
                data: RagStepHelper.BuildRetrievalStepResultData(batch, providerKey));
        }
    }
}