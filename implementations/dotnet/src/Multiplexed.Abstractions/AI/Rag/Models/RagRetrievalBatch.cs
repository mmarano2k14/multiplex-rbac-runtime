using System;
using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Rag.Models
{
    /// <summary>
    /// Represents the result of a retrieval execution.
    ///
    /// PURPOSE:
    /// - Encapsulates normalized items returned by providers.
    /// - Provides batch-level metadata for orchestration (merge, rank, tracing).
    /// - Supports deterministic replay and distributed execution.
    ///
    /// DESIGN:
    /// - Immutable.
    /// - Safe for distributed execution and replay.
    /// - Metadata is optional and additive (non-breaking).
    /// </summary>
    public sealed class RagRetrievalBatch
    {
        /// <summary>
        /// Gets the provider key that produced this batch.
        ///
        /// EXAMPLES:
        /// - "sql"
        /// - "redis-vector"
        /// - "api"
        /// </summary>
        public string? ProviderKey { get; init; }

        /// <summary>
        /// Gets the logical query key associated with this retrieval.
        ///
        /// This usually matches the operation key.
        /// </summary>
        public string? QueryKey { get; init; }

        /// <summary>
        /// Gets the original query text (if applicable).
        ///
        /// Used for:
        /// - tracing
        /// - debugging
        /// - downstream LLM prompts
        /// </summary>
        public string? QueryText { get; init; }

        /// <summary>
        /// Gets the normalized items.
        /// </summary>
        public IReadOnlyList<RagNormalizedItem> Items { get; init; }
            = Array.Empty<RagNormalizedItem>();

        /// <summary>
        /// Gets optional diagnostics information.
        /// </summary>
        public RagRetrievalDiagnostics? Diagnostics { get; init; }

        /// <summary>
        /// Gets optional metadata associated with the batch.
        ///
        /// Can include:
        /// - timing
        /// - source hints
        /// - provider-specific information
        ///
        /// IMPORTANT:
        /// - Must remain serializable
        /// - Must not be required for core orchestration
        /// </summary>
        public IReadOnlyDictionary<string, object?> Metadata { get; init; }
            = new Dictionary<string, object?>();
    }
}