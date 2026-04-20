using System;
using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Rag.Models
{
    /// <summary>
    /// Represents aggregated diagnostics for a RAG retrieval execution.
    ///
    /// PURPOSE:
    /// - Provides global visibility across all providers.
    /// - Tracks execution metrics and transformation stages.
    /// - Enables observability, monitoring, and replay analysis.
    ///
    /// DESIGN:
    /// - Built after the full retrieval pipeline completes.
    /// - Aggregates per-provider diagnostics.
    /// - Keeps transformation counts (merge, dedup, ranking).
    /// </summary>
    public sealed class RagRetrievalDiagnostics
    {
        public int TotalProviders { get; init; }
        public int SuccessfulProviders { get; init; }
        public int FailedProviders { get; init; }

        public int RawItemCount { get; init; }
        public int AfterMergeCount { get; init; }
        public int AfterDedupCount { get; init; }
        public int FinalItemCount { get; init; }

        public long TotalDurationMs { get; init; }

        public IReadOnlyList<RagProviderExecutionDiagnostics> Providers { get; init; }
            = Array.Empty<RagProviderExecutionDiagnostics>();
    }
}