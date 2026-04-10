using System;
using System.Text.Json;
using Multiplexed.Abstractions.AI.Prompt;

namespace Multiplexed.AI.Runtime.AI.Prompt
{
    /// <summary>
    /// Default implementation of <see cref="IAiPromptResultParser"/>.
    ///
    /// Current behavior:
    /// - If the expected format is "json", the parser attempts to deserialize
    ///   the raw text into a <see cref="JsonElement"/>.
    /// - Otherwise, the parser returns the raw text unchanged.
    ///
    /// This keeps parsing deterministic while remaining easy to evolve later.
    /// </summary>
    public sealed class DefaultAiPromptResultParser : IAiPromptResultParser
    {
        /// <inheritdoc />
        public object? Parse(string rawText, string? responseFormat = null)
        {
            ArgumentNullException.ThrowIfNull(rawText);

            if (!string.Equals(responseFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                return rawText;
            }

            using var document = JsonDocument.Parse(rawText);

            return document.RootElement.Clone();
        }
    }
}