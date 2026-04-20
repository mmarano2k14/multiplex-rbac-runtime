using System;

namespace Multiplexed.Abstractions.AI.Rag.Models
{
    /// <summary>
    /// Represents the execution diagnostics of a single RAG provider.
    ///
    /// PURPOSE:
    /// - Captures per-provider execution metrics.
    /// - Enables observability at the provider level.
    /// - Supports debugging, performance analysis, and replay validation.
    ///
    /// DESIGN:
    /// - Immutable structure.
    /// - Contains only execution-related information (no business data).
    /// </summary>
    public sealed class RagProviderExecutionDiagnostics
    {
        /// <summary>
        /// Gets the unique provider key.
        /// </summary>
        public string ProviderKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the provider execution succeeded.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets a value indicating whether fallback logic was triggered for this provider.
        /// </summary>
        public bool IsFallbackUsed { get; init; }

        /// <summary>
        /// Gets the number of items returned by the provider.
        /// </summary>
        public int ItemCount { get; init; }

        /// <summary>
        /// Gets the execution duration in milliseconds.
        /// </summary>
        public long DurationMs { get; init; }

        /// <summary>
        /// Gets the error message if the provider failed.
        /// </summary>
        public string? Error { get; init; }
    }
}