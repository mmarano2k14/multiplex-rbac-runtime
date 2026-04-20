using System.Collections.Generic;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval
{
    /// <summary>
    /// Merges multiple retrieval batches into a single deterministic output batch.
    ///
    /// PURPOSE:
    /// - Supports expert DAG mode where retrieval is split across multiple steps.
    /// - Centralizes deterministic merge behavior outside of step orchestration.
    ///
    /// DESIGN:
    /// - Input batches are assumed to be individually valid and serializable.
    /// - Output must remain deterministic and replay-safe.
    /// </summary>
    public interface IRagBatchMerger
    {
        /// <summary>
        /// Merges the provided batches into a single retrieval batch.
        /// </summary>
        /// <param name="batches">
        /// The retrieval batches to merge.
        /// </param>
        /// <returns>
        /// A single deterministic retrieval batch.
        /// </returns>
        RagRetrievalBatch Merge(IReadOnlyList<RagRetrievalBatch> batches);
    }
}