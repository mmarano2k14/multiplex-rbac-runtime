using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Result of a retrieval operation.
    /// Can aggregate multiple providers.
    /// </summary>
    public sealed class RagRetrievalBatch
    {
        public IReadOnlyList<RagNormalizedItem> Items { get; init; }
            = Array.Empty<RagNormalizedItem>();
    }
}