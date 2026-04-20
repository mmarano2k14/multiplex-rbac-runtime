using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Attributes;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors
{
    /// <summary>
    /// Represents the discovered metadata for a RAG composer implementation.
    ///
    /// PURPOSE:
    /// - Stores the normalized discovery result extracted from
    ///   <see cref="RagComposerAttribute"/>.
    /// - Provides a stable descriptor model for registries, dependency injection,
    ///   diagnostics, and tooling.
    /// - Avoids repeated reflection over composer implementation types.
    ///
    /// DESIGN:
    /// - This type contains metadata only.
    /// - It describes the implementation class and its semantic identity,
    ///   but does not instantiate or execute the composer.
    ///
    /// USAGE:
    /// - Produced by the discovery layer when scanning assemblies.
    /// - Stored in composer registries for lookup by key or kind.
    /// - Used later by runtime resolution and configuration flows.
    /// </summary>
    public sealed class RagComposerDescriptor
    {
        /// <summary>
        /// Gets or initializes the unique composer key used for configuration,
        /// lookup, and runtime resolution.
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the concrete implementation type that was decorated
        /// with <see cref="RagComposerAttribute"/>.
        /// </summary>
        public Type ImplementationType { get; init; } = default!;

        /// <summary>
        /// Gets or initializes the semantic composer kind implemented by the
        /// decorated class.
        /// </summary>
        public RagComposerKind Kind { get; init; }

        /// <summary>
        /// Gets or initializes the optional human-readable display name.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether the composer is marked
        /// as the default implementation for its category.
        /// </summary>
        public bool IsDefault { get; init; }
    }
}