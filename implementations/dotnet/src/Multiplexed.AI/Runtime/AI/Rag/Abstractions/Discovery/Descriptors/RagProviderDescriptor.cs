using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Attributes;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors
{
    /// <summary>
    /// Represents the discovered metadata for a RAG provider implementation.
    ///
    /// PURPOSE:
    /// - Stores the normalized discovery result extracted from
    ///   <see cref="RagProviderAttribute"/>.
    /// - Provides a stable runtime descriptor that can be consumed by registries,
    ///   dependency injection, diagnostics, and tooling layers.
    /// - Avoids repeated reflection over provider implementation types once
    ///   discovery has already been completed.
    ///
    /// DESIGN:
    /// - This type is metadata-only and does not create or own provider instances.
    /// - It separates discovery-time information from runtime resolution concerns.
    /// - The descriptor keeps both semantic information and the concrete
    ///   implementation type.
    ///
    /// USAGE:
    /// - Produced by discovery services during assembly scanning.
    /// - Stored in provider registries for lookup by key or category.
    /// - Used later by DI registration and selection policies.
    /// </summary>
    public sealed class RagProviderDescriptor
    {
        /// <summary>
        /// Gets or initializes the unique provider key used for configuration,
        /// lookup, and runtime resolution.
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the concrete implementation type that was decorated
        /// with <see cref="RagProviderAttribute"/>.
        /// </summary>
        public Type ImplementationType { get; init; } = default!;

        /// <summary>
        /// Gets or initializes the functional provider kind.
        /// </summary>
        public RagProviderKind ProviderKind { get; init; }

        /// <summary>
        /// Gets or initializes the concrete provider source type.
        /// </summary>
        public RagProviderSourceType SourceType { get; init; }

        /// <summary>
        /// Gets or initializes the optional human-readable display name.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether the provider is marked
        /// as the default implementation for its category.
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Gets or initializes the declared provider status.
        /// </summary>
        public RagProviderStatus Status { get; init; }
    }
}