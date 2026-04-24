using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;
using Multiplexed.AI.Runtime.AI.Rag.Steps;

/// <summary>
/// Merges multiple retrieval batches into a single deterministic batch.
///
/// PURPOSE:
/// - Reads upstream retrieval batches from configured source steps.
/// - Supports both inline and payload-backed upstream batches.
/// - Delegates merge behavior to the configured <see cref="IRagBatchMerger"/>.
/// - Produces a consistent serializable batch for downstream steps.
///
/// CONFIG:
/// - sourceSteps: list of upstream step names exposing result.data.batch (required)
///
/// CONTRACT:
/// - Each source step must exist in the execution state.
/// - Each step must expose a valid <c>result.data.batch</c>, either inline or through DataPayloads.
///
/// OUTPUT:
/// - data:
///     - batch: merged <see cref="RagRetrievalBatch"/>
///     - itemCount: number of items in the merged batch
///     - diagnostics: aggregated diagnostics
///
/// DETERMINISM:
/// - Merge must produce identical output for identical inputs.
/// - Output ordering is normalized and independent from caller order.
/// - StableOrder is reassigned to a dense sequence (0..N-1).
///
/// SAFETY:
/// - Fails fast when upstream data is invalid.
/// - Protects downstream composition steps from inconsistent ordering.
/// </summary>
[AiStep("rag.merge")]
public sealed class RagMergeStep : IAiStep
{
    private readonly IRagBatchMerger _batchMerger;

    public RagMergeStep(IRagBatchMerger batchMerger)
    {
        _batchMerger = batchMerger ?? throw new ArgumentNullException(nameof(batchMerger));
    }

    public string Name => "rag.merge";

    public async Task<AiStepResult> ExecuteAsync(
        AiStepExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sourceSteps = RagStepHelper.GetRequiredSourceSteps(context);

        var batches = new List<RagRetrievalBatch>(sourceSteps.Count);

        foreach (var stepName in sourceSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await RagStepHelper.GetRequiredBatchAsync(
                context,
                stepName,
                cancellationToken);

            batches.Add(batch);
        }

        var merged = _batchMerger.Merge(batches);

        ValidateMergedBatch(merged);

        return AiStepResult.Ok(
            output: $"Merged {batches.Count} batch(es) into {merged.Items.Count} item(s).",
            data: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["batch"] = merged,
                ["itemCount"] = merged.Items.Count,
                ["diagnostics"] = merged.Diagnostics
            });
    }

    /// <summary>
    /// Validates structural invariants expected from a merged retrieval batch.
    ///
    /// INVARIANTS:
    /// - Batch must not be null.
    /// - Items must not be null.
    /// - No item may be null.
    /// - StableOrder must be dense and sequential (0..N-1).
    /// </summary>
    private static void ValidateMergedBatch(RagRetrievalBatch merged)
    {
        ArgumentNullException.ThrowIfNull(merged);

        if (merged.Items is null)
        {
            throw new InvalidOperationException(
                "rag.merge: Merged batch contains null Items.");
        }

        for (var i = 0; i < merged.Items.Count; i++)
        {
            var item = merged.Items[i];

            if (item is null)
            {
                throw new InvalidOperationException(
                    $"rag.merge: Null item detected at index {i}.");
            }

            if (item.StableOrder != i)
            {
                throw new InvalidOperationException(
                    $"rag.merge: Invalid StableOrder at index {i}. Expected {i}, found {item.StableOrder}.");
            }
        }
    }
}