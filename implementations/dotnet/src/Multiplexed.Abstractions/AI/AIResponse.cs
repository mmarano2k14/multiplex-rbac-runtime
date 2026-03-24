using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI
{
    public sealed class AIResponse
    {
        public string Content { get; init; } = string.Empty;

        // futur
        public int? TokenUsage { get; init; }
        public string? Model { get; init; }
        public TimeSpan? Duration { get; init; }
    }
}
