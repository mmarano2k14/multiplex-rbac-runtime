using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Prompt.Models
{
    /// <summary>
    /// Represents the high-level prompt execution request used by the runtime.
    ///
    /// This model is provider-agnostic and safe to persist in execution state,
    /// metadata, logs, or snapshots.
    /// </summary>
    public sealed class AiPromptRequest
    {
        /// <summary>
        /// Gets or sets the logical provider key.
        /// </summary>
        public string ProviderKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target model identifier.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the prompt template before rendering.
        /// </summary>
        public string PromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the variables used to render the prompt template.
        /// </summary>
        public Dictionary<string, object?> Variables { get; set; } = new();

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
        ///
        /// Example values:
        /// - text
        /// - json
        /// </summary>
        public string? ResponseFormat { get; set; }

        /// <summary>
        /// Gets or sets the optional prompt version.
        ///
        /// This is useful for auditability, reproducibility, and replay analysis.
        /// </summary>
        public string? PromptVersion { get; set; }

        /// <summary>
        /// Gets or sets additional provider-agnostic metadata.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }
}