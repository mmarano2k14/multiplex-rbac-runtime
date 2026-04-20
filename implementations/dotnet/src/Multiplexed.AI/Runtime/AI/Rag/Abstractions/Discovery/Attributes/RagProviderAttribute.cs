using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Attributes
{
    /// <summary>
    /// Marks a class as a discoverable RAG provider implementation.
    ///
    /// PURPOSE:
    /// - Enables reflection-based discovery of provider implementations.
    /// - Exposes stable metadata used during registration and runtime lookup.
    /// - Aligns provider discovery with the same architectural pattern already
    ///   used for step implementations and LLM providers.
    ///
    /// DESIGN:
    /// - The attribute carries both a stable external <see cref="Key"/> and
    ///   semantic enum-based metadata.
    /// - <see cref="RagProviderKind"/> describes the functional provider role.
    /// - <see cref="RagProviderSourceType"/> describes the concrete source or
    ///   implementation category.
    ///
    /// USAGE:
    /// - Applied to concrete provider classes such as Redis vector providers,
    ///   SQL providers, or runtime state providers.
    /// - Consumed by discovery and DI registration layers to build descriptors
    ///   and registries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RagProviderAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RagProviderAttribute"/> class.
        /// </summary>
        /// <param name="key">
        /// The unique external key used to identify the provider in configuration,
        /// discovery registries, and runtime resolution.
        /// </param>
        /// <param name="providerKind">
        /// The functional provider kind implemented by the decorated class.
        /// </param>
        /// <param name="sourceType">
        /// The concrete provider source type implemented by the decorated class.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="key"/> is empty or whitespace.
        /// </exception>
        public RagProviderAttribute(
            string key,
            RagProviderKind providerKind,
            RagProviderSourceType sourceType)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "The RAG provider key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            Key = key;
            ProviderKind = providerKind;
            SourceType = sourceType;
        }

        /// <summary>
        /// Gets the unique provider key used for configuration and runtime lookup.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the functional provider kind implemented by the decorated class.
        /// </summary>
        public RagProviderKind ProviderKind { get; }

        /// <summary>
        /// Gets the concrete provider source type implemented by the decorated class.
        /// </summary>
        public RagProviderSourceType SourceType { get; }

        /// <summary>
        /// Gets or initializes an optional human-readable display name for the provider.
        ///
        /// This is useful for diagnostics, registry inspection, and tooling.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether this provider should be
        /// considered the default implementation for its category.
        ///
        /// IMPORTANT:
        /// This is only metadata. Actual resolution policy remains the responsibility
        /// of the registry or selection layer.
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Gets or initializes the runtime status associated with the provider.
        ///
        /// This allows discovery layers to expose whether a provider is enabled,
        /// experimental, deprecated, or otherwise constrained.
        /// </summary>
        public RagProviderStatus Status { get; init; } = RagProviderStatus.Enabled;
    }
}