using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.Execution.Context;

/// <summary>
/// Pipeline step that composes a final deterministic context from a retrieval batch.
///
/// PURPOSE:
/// - Acts as the expert DAG entry point for context composition.
/// - Reads a previously merged or directly retrieved batch from pipeline state.
/// - Produces a serializable composed context for downstream prompt steps.
///
/// CONFIG:
/// - sourceStep: upstream step name containing a result.data.batch entry (required)
/// - composer: composer key used to resolve an <see cref="IRagComposer"/> (required)
///
/// CONTRACT:
/// - The specified <c>sourceStep</c> must exist in the execution state.
/// - The specified step must expose <c>result.data.batch</c>.
/// - The specified <c>composer</c> must resolve to a registered composer.
///
/// OUTPUT:
/// - output: composed context text (best-effort string)
/// - data:
///     - context: <see cref="RagStructuredContext"/>
///     - fragments: ordered fragment list
///
/// DETERMINISM:
/// - Composition must be deterministic for identical input batches.
/// - Fragment ordering must remain stable and reproducible.
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

        var helper = context.GetHelper();

        var sourceStep = await helper.GetRequiredConfigAsync<string>(
            "sourceStep",
            cancellationToken);

        var composerKey = await helper.GetRequiredConfigAsync<string>(
            "composer",
            cancellationToken);

        var batch = await helper.GetRequiredBatchAsync(
            sourceStep,
            cancellationToken);

        var composer = _composerResolver.Resolve(composerKey);

        var composed = await composer.ComposeAsync(batch, cancellationToken);

        return AiStepResult.Ok(
            output: composed.Context?.Text ?? string.Empty,
            //data: helper.ToDictionary(composed, ignoreNull: false));
            data: helper.ToDictionary(new
            {
                context = composed.Context,
                fragments = composed.Fragments,
                fragmentCount = composed.Fragments.Count,
                metadata = composed.Metadata
            }, ignoreNull: false));
    }
}