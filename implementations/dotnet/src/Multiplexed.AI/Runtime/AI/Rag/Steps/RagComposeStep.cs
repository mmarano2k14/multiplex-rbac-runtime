using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Pipeline step that composes a final deterministic context from a retrieval batch.
    ///
    /// PURPOSE:
    /// - Acts as the expert DAG entry point for context composition.
    /// - Reads a previously merged or directly retrieved batch from pipeline state.
    /// - Produces a serializable composed context for downstream prompt steps.
    ///
    /// CONFIG:
    /// - sourceStep: upstream step name containing a result.data.batch entry
    /// </summary>
    [AiStep("rag.compose")]
    public sealed class RagComposeStep : IAiStep
    {
        private readonly IRagComposerResolver _composerResolver;

        public RagComposeStep(IRagComposerResolver composerResolver)
        {
            _composerResolver = composerResolver ?? throw new ArgumentNullException(nameof(composerResolver));
        }

        public string Name => "rag.compose";

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetStepConfigValue<string>("sourceStep", out var sourceStep) ||
                string.IsNullOrWhiteSpace(sourceStep))
            {
                throw new InvalidOperationException("Missing 'sourceStep'.");
            }

            if (!context.TryGetStepConfigValue<string>("composer", out var composerKey) ||
                string.IsNullOrWhiteSpace(composerKey))
            {
                throw new InvalidOperationException("Missing 'composer'.");
            }

            // 🔥 CENTRALIZED batch resolution
            var batch = RagStepHelper.GetRequiredBatch(context, sourceStep);

            // 🔥 dynamic composer resolution
            var composer = _composerResolver.Resolve(composerKey);

            var composed = await composer.ComposeAsync(batch, cancellationToken);

            return AiStepResult.Ok(
                output: composed.Context?.Text ?? string.Empty,
                data: RagStepHelper.BuildCompositionStepResultData(composed));
        }
    }
}