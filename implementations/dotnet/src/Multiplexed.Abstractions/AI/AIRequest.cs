using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI
{
    public sealed class AIRequest
    {
        public string Prompt { get; init; } = string.Empty;

        // futur
        public string? SystemPrompt { get; init; }
        public float? Temperature { get; init; }
        public int? MaxTokens { get; init; }
    }
}
