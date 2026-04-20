using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Represents the result of a retrieval execution.
    ///
    /// PURPOSE:
    /// - Encapsulates normalized items returned by providers.
    /// - Optionally includes diagnostics for observability.
    ///
    /// DESIGN:
    /// - Immutable.
    /// - Safe for distributed execution and replay.
    /// </summary>
    public sealed class RagRetrievalBatch
    {
        /// <summary>
        /// Gets the normalized items.
        /// </summary>
        public IReadOnlyList<RagNormalizedItem> Items { get; init; }
            = Array.Empty<RagNormalizedItem>();

        /// <summary>
        /// Gets optional diagnostics information.
        /// </summary>
        public RagRetrievalDiagnostics? Diagnostics { get; init; }
    }
}