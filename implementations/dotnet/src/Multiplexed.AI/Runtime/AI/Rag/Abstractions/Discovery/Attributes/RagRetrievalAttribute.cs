using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery
{
    /// <summary>
    /// Marks a class as a discoverable RAG retrieval implementation.
    ///
    /// PURPOSE:
    /// - Enables reflection-based discovery of retrieval implementations.
    /// - Provides stable metadata for runtime registration and lookup.
    /// - Distinguishes retrieval orchestration from provider implementations.
    ///
    /// DESIGN:
    /// - The attribute exposes a stable external <see cref="Key"/> for config
    ///   and runtime resolution.
    /// - <see cref="RagRetrievalKind"/> identifies the retrieval strategy
    ///   represented by the decorated class.
    ///
    /// USAGE:
    /// - Applied to concrete retrieval implementations such as vector retrieval,
    ///   SQL retrieval, runtime retrieval, or multi-provider retrieval.
    /// - Consumed by discovery layers to build descriptors and registries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RagRetrievalAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RagRetrievalAttribute"/> class.
        /// </summary>
        /// <param name="key">
        /// The unique external key used to identify the retrieval implementation.
        /// </param>
        /// <param name="kind">
        /// The semantic retrieval kind implemented by the decorated class.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="key"/> is null, empty, or whitespace.
        /// </exception>
        public RagRetrievalAttribute(string key, RagRetrievalKind kind)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "The RAG retrieval key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            Key = key;
            Kind = kind;
        }

        /// <summary>
        /// Gets the unique retrieval key used for configuration and runtime lookup.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the semantic retrieval kind implemented by the decorated class.
        /// </summary>
        public RagRetrievalKind Kind { get; }

        /// <summary>
        /// Gets or initializes an optional human-readable display name.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether this retrieval should be
        /// considered the default implementation for its category.
        /// </summary>
        public bool IsDefault { get; init; }
    }
}