using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Prompt.Models
{
    /// <summary>
    /// Represents the normalized prompt execution result returned by the runtime.
    ///
    /// This object must remain serializable, provider-agnostic, and replay-safe.
    /// No SDK-specific types should ever be stored here.
    /// </summary>
    public sealed class AiPromptResult
    {
        /// <summary>
        /// Gets or sets the logical provider key that was used.
        /// </summary>
        public string ProviderKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model identifier that was used.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the raw text returned by the provider.
        /// </summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parsed result, if structured parsing was applied.
        /// </summary>
        public object? ParsedResult { get; set; }

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
        /// Gets or sets the prompt version associated with this execution, if available.
        /// </summary>
        public string? PromptVersion { get; set; }

        /// <summary>
        /// Gets or sets the hash of the final rendered prompt.
        ///
        /// This is useful for replay, audit, caching, and debugging without
        /// necessarily storing the entire rendered prompt everywhere.
        /// </summary>
        public string? RenderedPromptHash { get; set; }

        /// <summary>
        /// Gets or sets additional normalized metadata.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }
}