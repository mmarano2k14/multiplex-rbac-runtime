using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Pipeline step that executes a single SQL-oriented RAG provider.
    ///
    /// PURPOSE:
    /// - Acts as the expert DAG entry point for SQL retrieval.
    /// - Resolves the configured provider and delegates execution.
    /// - Returns a serializable retrieval batch for downstream merge steps.
    /// </summary>
    [AiStep("rag.sql")]
    public sealed class RagSqlStep : IAiStep
    {
        private readonly INormalizingRagProviderResolver _providerResolver;

        public RagSqlStep(INormalizingRagProviderResolver providerResolver)
        {
            _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        }

        public string Name => "rag.sql";

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
                output: $"SQL retrieval completed with {batch.Items.Count} item(s).",
                data: RagStepHelper.BuildRetrievalStepResultData(batch, providerKey));
        }
    }
}