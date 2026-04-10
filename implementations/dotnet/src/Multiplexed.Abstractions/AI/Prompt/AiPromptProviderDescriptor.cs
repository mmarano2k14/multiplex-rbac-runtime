using System;

namespace Multiplexed.Abstractions.AI.Prompt
{
    /// <summary>
    /// Describes a discovered AI prompt provider implementation.
    ///
    /// PURPOSE:
    /// - Stores the logical provider key declared by attribute
    /// - Stores the concrete implementation type
    /// - Acts as the registry unit for provider resolution
    /// </summary>
    public sealed class AiPromptProviderDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiPromptProviderDescriptor"/> class.
        /// </summary>
        /// <param name="providerKey">The logical provider key.</param>
        /// <param name="implementationType">The concrete provider implementation type.</param>
        public AiPromptProviderDescriptor(
            string providerKey,
            Type implementationType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
            ArgumentNullException.ThrowIfNull(implementationType);

            ProviderKey = providerKey;
            ImplementationType = implementationType;
        }

        /// <summary>
        /// Gets the logical provider key.
        /// </summary>
        public string ProviderKey { get; }

        /// <summary>
        /// Gets the concrete provider implementation type.
        /// </summary>
        public Type ImplementationType { get; }
    }
}