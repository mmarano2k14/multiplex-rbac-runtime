using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Prompt.Models
{
    /// <summary>
    /// Represents the low-level provider request produced after prompt rendering.
    ///
    /// This request contains the final rendered prompt string that will be sent
    /// to the selected provider.
    /// </summary>
    public sealed class AiPromptProviderRequest
    {
        /// <summary>
        /// Gets or sets the target model identifier.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the fully rendered prompt to send to the provider.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional temperature value.
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// Gets or sets the optional maximum number of output tokens.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Gets or sets the expected response format.
        /// </summary>
        public string? ResponseFormat { get; set; }

        /// <summary>
        /// Gets or sets additional provider-agnostic metadata.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }
}