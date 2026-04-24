namespace Multiplexed.Abstractions.AI.Memory
{
    /// <summary>
    /// Represents a durable consolidated memory produced from execution experience.
    ///
    /// PURPOSE:
    /// - Stores useful long-term knowledge derived from executions
    /// - Keeps memory separate from the execution ledger
    /// - Supports scoring, recall reinforcement, and natural decay
    ///
    /// IMPORTANT:
    /// - This record is NOT the source of truth for replay
    /// - Replay must continue to depend on the execution ledger and payload artifacts
    /// - Consolidated memory is derived, compressed, and allowed to evolve over time
    /// </summary>
    public sealed class AiConsolidatedMemoryRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Scope { get; set; } = "default";

        public string Kind { get; set; } = "semantic";

        public string Content { get; set; } = string.Empty;

        public double InitialScore { get; set; }

        public double CurrentScore { get; set; }

        public double TaskRelevance { get; set; }

        public double Novelty { get; set; }

        public double Confidence { get; set; }

        public int AccessCount { get; set; }

        public int AgeInSessions { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public List<string> ProvenanceExecutionIds { get; set; } = [];

        public List<string> ProvenanceStepNames { get; set; } = [];

        public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.Ordinal);
    }
}