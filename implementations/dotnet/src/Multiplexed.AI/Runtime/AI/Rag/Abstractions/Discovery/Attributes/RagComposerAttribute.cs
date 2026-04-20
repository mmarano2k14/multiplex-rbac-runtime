using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Attributes
{
    /// <summary>
    /// Marks a class as a discoverable RAG composer implementation.
    ///
    /// PURPOSE:
    /// - Enables reflection-based discovery of composer implementations.
    /// - Exposes stable metadata for DI registration and runtime lookup.
    /// - Keeps composition concerns clearly separated from retrieval and provider
    ///   concerns.
    ///
    /// DESIGN:
    /// - The attribute carries a stable external <see cref="Key"/> and a semantic
    ///   <see cref="RagComposerKind"/>.
    /// - The key is intended for config and resolution, while the enum expresses
    ///   the functional meaning of the implementation.
    ///
    /// USAGE:
    /// - Applied to concrete composer implementations such as structured or
    ///   deterministic composers.
    /// - Consumed by discovery services to build composer descriptors and registries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RagComposerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RagComposerAttribute"/> class.
        /// </summary>
        /// <param name="key">
        /// The unique external key used to identify the composer implementation.
        /// </param>
        /// <param name="kind">
        /// The semantic composer kind implemented by the decorated class.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="key"/> is null, empty, or whitespace.
        /// </exception>
        public RagComposerAttribute(string key, RagComposerKind kind)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "The RAG composer key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            Key = key;
            Kind = kind;
        }

        /// <summary>
        /// Gets the unique composer key used for configuration and runtime lookup.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the semantic composer kind implemented by the decorated class.
        /// </summary>
        public RagComposerKind Kind { get; }

        /// <summary>
        /// Gets or initializes an optional human-readable display name.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether this composer should be
        /// considered the default implementation for its category.
        /// </summary>
        public bool IsDefault { get; init; }
    }
}