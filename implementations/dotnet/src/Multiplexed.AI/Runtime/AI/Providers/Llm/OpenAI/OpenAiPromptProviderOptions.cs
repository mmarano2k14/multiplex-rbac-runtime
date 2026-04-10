namespace Multiplexed.Abstractions.AI.Prompt.Options
{
    /// <summary>
    /// Configuration options for the OpenAI prompt provider.
    ///
    /// PURPOSE:
    /// - Supplies API access settings to the concrete OpenAI provider
    /// - Keeps provider construction explicit and configuration-driven
    /// - Supports future extension without changing the provider contract
    /// </summary>
    public sealed class OpenAiPromptProviderOptions
    {
        /// <summary>
        /// Gets or sets the API key used to authenticate with OpenAI.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional custom endpoint.
        ///
        /// This can be used for compatible gateways or future routing scenarios.
        /// When null or empty, the default OpenAI endpoint is used.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the optional organization identifier.
        ///
        /// Keep nullable unless you explicitly need it.
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Gets or sets the optional project identifier.
        ///
        /// Keep nullable unless you explicitly need it.
        /// </summary>
        public string? Project { get; set; }
    }
}