using System;

namespace Multiplexed.Abstractions.AI.Prompt
{
    /// <summary>
    /// Identifies a concrete AI prompt provider implementation with a stable logical key.
    ///
    /// PURPOSE:
    /// - Supports assembly scanning and automatic DI registration
    /// - Provides a stable provider identity independent from the concrete type name
    /// - Keeps provider selection configuration-driven at runtime
    ///
    /// EXAMPLE:
    /// <code>
    /// [AiPromptProvider("openai")]
    /// public sealed class OpenAiPromptProvider : IAiPromptProvider
    /// {
    ///     ...
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AiPromptProviderAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiPromptProviderAttribute"/> class.
        /// </summary>
        /// <param name="providerKey">
        /// The stable logical key used to resolve the provider.
        /// </param>
        public AiPromptProviderAttribute(string providerKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
            ProviderKey = providerKey;
        }

        /// <summary>
        /// Gets the stable logical provider key.
        /// </summary>
        public string ProviderKey { get; }
    }
}