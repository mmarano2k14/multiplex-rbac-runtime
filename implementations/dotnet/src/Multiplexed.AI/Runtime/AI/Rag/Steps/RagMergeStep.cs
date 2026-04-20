using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Merges multiple retrieval batches into a single deterministic batch.
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

        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var sourceSteps = RagStepHelper.GetRequiredSourceSteps(context);
            var batches = new List<RagRetrievalBatch>(sourceSteps.Count);

            foreach (var stepName in sourceSteps)
            {
                var batch = RagStepHelper.GetRequiredBatch(context, stepName);
                batches.Add(batch);
            }

            var merged = _batchMerger.Merge(batches);

            return Task.FromResult(
                AiStepResult.Ok(
                    output: $"Merged {batches.Count} batch(es) into {merged.Items.Count} item(s).",
                    data: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["batch"] = merged,
                        ["itemCount"] = merged.Items.Count,
                        ["diagnostics"] = merged.Diagnostics
                    }));
        }
    }
}