using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Attributes;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors
{
    /// <summary>
    /// Represents the discovered metadata for a RAG retrieval implementation.
    ///
    /// PURPOSE:
    /// - Stores the normalized discovery result extracted from
    ///   <see cref="RagRetrievalAttribute"/>.
    /// - Provides a stable runtime descriptor that can be used by registries,
    ///   dependency injection, and diagnostics.
    /// - Prevents repeated attribute reflection once discovery has completed.
    ///
    /// DESIGN:
    /// - This descriptor is metadata-only.
    /// - It describes a retrieval implementation, not an instance.
    /// - It keeps the stable external key and the semantic retrieval kind
    ///   alongside the concrete implementation type.
    ///
    /// USAGE:
    /// - Produced during assembly scanning by the RAG discovery layer.
    /// - Registered in retrieval registries for lookup and resolution.
    /// - Used later by orchestration or configuration code to select the
    ///   appropriate retrieval implementation.
    /// </summary>
    public sealed class RagRetrievalDescriptor
    {
        /// <summary>
        /// Gets or initializes the unique retrieval key used for configuration,
        /// lookup, and runtime resolution.
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the concrete implementation type that was decorated
        /// with <see cref="RagRetrievalAttribute"/>.
        /// </summary>
        public Type ImplementationType { get; init; } = default!;

        /// <summary>
        /// Gets or initializes the semantic retrieval kind implemented by the
        /// decorated class.
        /// </summary>
        public RagRetrievalKind Kind { get; init; }

        /// <summary>
        /// Gets or initializes the optional human-readable display name.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether the retrieval is marked
        /// as the default implementation for its category.
        /// </summary>
        public bool IsDefault { get; init; }
    }
}