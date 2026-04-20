using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Pipeline step that executes a configured multi-provider RAG retrieval strategy.
    ///
    /// PURPOSE:
    /// - Acts as the compact DAG entry point for multi-provider retrieval.
    /// - Builds a normalized RAG execution context from the current step execution context.
    /// - Delegates orchestration to <see cref="IRagRetrieval"/>.
    ///
    /// DESIGN:
    /// - This step is orchestration-facing, not provider-facing.
    /// - Provider execution, merge, deduplication, ranking, and diagnostics
    ///   are delegated to the retrieval service.
    /// - The step returns a serializable retrieval batch suitable for persistence,
    ///   replay, downstream merge, or composition.
    ///
    /// IMPORTANT:
    /// - This step does not contain provider-specific logic.
    /// - Replay safety depends on persisting the exact retrieval batch result.
    /// - The retrieval service must remain deterministic for identical inputs.
    /// </summary>
    [AiStep("rag.multi")]
    public sealed class RagMultiStep : IAiStep
    {
        private readonly IRagRetrievalResolver _retrievalResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="RagMultiStep"/> class.
        /// </summary>
        /// <param name="retrievalResolver">
        /// The resolver responsible for loading the configured retrieval strategy.
        /// </param>
        public RagMultiStep(IRagRetrievalResolver retrievalResolver)
        {
            _retrievalResolver = retrievalResolver ?? throw new ArgumentNullException(nameof(retrievalResolver));
        }

        /// <summary>
        /// Gets the stable registry key for this step type.
        /// </summary>
        public string Name => "rag.multi";

        /// <inheritdoc />
        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            // Resolve the retrieval strategy key from the current step configuration.
            if (!context.TryGetStepConfigValue<string>("retrieval", out var retrievalKey) ||
                string.IsNullOrWhiteSpace(retrievalKey))
            {
                throw new InvalidOperationException(
                    "The current step configuration is missing required field 'retrieval'.");
            }

            // Resolve the configured retrieval strategy through the runtime resolver.
            var retrieval = _retrievalResolver.Resolve(retrievalKey);

            // Build the generic RAG execution envelope from the current step.
            var ragContext = RagStepHelper.BuildRagExecutionContext(context);

            // Delegate the full orchestration to the retrieval strategy.
            var batch = await retrieval.RetrieveAsync(ragContext, cancellationToken);

            return AiStepResult.Ok(
                output: $"Retrieval '{retrievalKey}' completed with {batch.Items.Count} item(s).",
                data: BuildStepResultData(batch, retrievalKey));
        }

        /// <summary>
        /// Builds the serializable step result data bag returned to the runtime.
        ///
        /// PURPOSE:
        /// - Keeps the output structure stable for persistence and replay.
        /// - Exposes the retrieval batch and diagnostics to downstream steps.
        /// </summary>
        private static Dictionary<string, object?> BuildStepResultData(
            Multiplexed.Abstractions.AI.Rag.Models.RagRetrievalBatch batch,
            string retrievalKey)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["retrieval"] = retrievalKey,
                ["itemCount"] = batch.Items.Count,
                ["batch"] = batch,
                ["diagnostics"] = batch.Diagnostics
            };
        }
    }
}