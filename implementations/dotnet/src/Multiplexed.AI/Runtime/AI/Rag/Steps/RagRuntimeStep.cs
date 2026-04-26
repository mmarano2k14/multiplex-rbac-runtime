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
                output: $"Runtime retrieval completed with {batch.Items.Count} item(s).",
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