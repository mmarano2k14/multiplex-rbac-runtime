using System;

namespace Multiplexed.AI.Runtime.AI.Prompt.Models
{
    /// <summary>
    /// Represents the normalized configuration consumed by the prompt pipeline step.
    ///
    /// This model acts as the bridge between declarative pipeline step configuration
    /// and the provider-agnostic prompt runtime.
    ///
    /// Typical JSON fields:
    /// - provider
    /// - model
    /// - template
    /// - temperature
    /// - maxTokens
    /// - responseFormat
    /// - promptVersion
    /// </summary>
    public sealed class AiPromptStepConfiguration
    {
        /// <summary>
        /// Gets or sets the logical provider key.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target model identifier.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the prompt template to render.
        /// </summary>
        public string Template { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional provider temperature.
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// Gets or sets the optional maximum output token count.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Gets or sets the expected response format.
        ///
        /// Example values:
        /// - text
        /// - json
        /// </summary>
        public string? ResponseFormat { get; set; }

        /// <summary>
        /// Gets or sets the optional prompt version.
        ///
        /// This is useful for auditability and replay diagnostics.
        /// </summary>
        public string? PromptVersion { get; set; }
    }
}