using System.Collections.Generic;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Normalized item used AFTER retrieval.
    /// 
    /// This is the bridge between:
    /// - strongly typed providers
    /// - generic orchestration
    /// </summary>
    public sealed class RagNormalizedItem
    {
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Provider identifier (ex: "redis-vector").
        /// </summary>
        public string ProviderKey { get; init; } = string.Empty;

        public RagProviderKind ProviderKind { get; init; }

        public RagProviderSourceType SourceType { get; init; }

        public string RetrievalKey { get; init; } = string.Empty;

        public RagRetrievalKind RetrievalKind { get; init; }

        public string ContentType { get; init; } = string.Empty;

        public string? ContentText { get; init; }

        public double? Score { get; init; }

        /// <summary>
        /// Original typed payload (optional).
        /// </summary>
        public object? Payload { get; init; }

        /// <summary>
        /// Deterministic ordering anchor.
        /// </summary>
        public int StableOrder { get; init; }

        public IReadOnlyDictionary<string, object?> Metadata { get; init; }
            = new Dictionary<string, object?>();
    }
}