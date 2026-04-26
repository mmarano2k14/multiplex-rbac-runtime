using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.Execution.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

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

            var helper = context.GetHelper();

            var providerKey = await helper.GetRequiredConfigAsync<string>(
                "provider",
                cancellationToken).ConfigureAwait(false);

            var provider = _providerResolver.Resolve(providerKey);

            var query = await helper.GetConfigAsync<string>(
                "query",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(query))
            {
                query = await helper.GetInputAsync<string>(
                    "query",
                    cancellationToken).ConfigureAwait(false);
            }

            var ragContext = new RagExecutionContext
            {
                QueryText = query ?? string.Empty,
                QueryKey = helper.StepKey,
                CorrelationId = helper.ExecutionId,
                Inputs = await helper.GetResolvedInputsAsync(
                    includeReservedVariables: true,
                    cancellationToken).ConfigureAwait(false),
                Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ExecutionId"] = helper.ExecutionId,
                    ["StepName"] = helper.StepName,
                    ["StepKey"] = helper.StepKey
                }
            };

            var batch = await provider.RetrieveNormalizedAsync(ragContext, cancellationToken);

            return AiStepResult.Ok(
                output: $"Vector retrieval completed with {batch.Items.Count} item(s).",
                data: helper.ToDictionary(new
                {
                    providerKey,
                    itemCount = batch.Items.Count,
                    batch,
                    diagnostics = batch.Diagnostics
                }, ignoreNull: true));
        }
    }
}