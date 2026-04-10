using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Prompt.Models
{
    /// <summary>
    /// Represents the normalized response returned by a concrete provider.
    ///
    /// This object hides SDK-specific response shapes while preserving the
    /// essential information needed by the runtime.
    /// </summary>
    public sealed class AiPromptProviderResponse
    {
        /// <summary>
        /// Gets or sets the raw text returned by the provider.
        /// </summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of input tokens used by the provider, if available.
        /// </summary>
        public int? InputTokens { get; set; }

        /// <summary>
        /// Gets or sets the number of output tokens used by the provider, if available.
        /// </summary>
        public int? OutputTokens { get; set; }

        /// <summary>
        /// Gets or sets the total number of tokens used by the provider, if available.
        /// </summary>
        public int? TotalTokens { get; set; }

        /// <summary>
        /// Gets or sets the provider finish reason, if available.
        /// </summary>
        public string? FinishReason { get; set; }

        /// <summary>
        /// Gets or sets provider-specific metadata in a normalized dictionary form.
        /// </summary>
        public Dictionary<string, object?> ProviderMetadata { get; set; } = new();
    }
}