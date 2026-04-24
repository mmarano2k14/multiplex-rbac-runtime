using Multiplexed.Abstractions.AI.Memory;

namespace Multiplexed.AI.Runtime.Memory
{
    /// <summary>
    /// Default deterministic memory scoring policy.
    ///
    /// PURPOSE:
    /// - Scores memory using simple deterministic signals
    /// - Models natural decay through the ratio of actual recall to expected recall
    /// - Avoids fixed TTLs and arbitrary age penalties
    ///
    /// MODEL:
    /// - High task relevance increases expected recall pressure
    /// - Memories that are not recalled enough naturally lose score
    /// - Novelty and confidence protect useful memories early
    ///
    /// IMPORTANT:
    /// - No LLM calls are used
    /// - This policy is deterministic for a given memory state
    /// </summary>
    public sealed class DefaultAiMemoryScoringPolicy : IAiMemoryScoringPolicy
    {
        public double ComputeInitialScore(AiConsolidatedMemoryRecord memory)
        {
            ArgumentNullException.ThrowIfNull(memory);

            var score =
                (memory.TaskRelevance * 0.45) +
                (memory.Novelty * 0.30) +
                (memory.Confidence * 0.25);

            return Clamp(score);
        }

        public double ComputeCurrentScore(AiConsolidatedMemoryRecord memory)
        {
            ArgumentNullException.ThrowIfNull(memory);

            var initialScore = memory.InitialScore > 0
                ? memory.InitialScore
                : ComputeInitialScore(memory);

            var expectedRecallPressure =
                1.0 + Math.Log(1 + Math.Max(0, memory.AgeInSessions)) *
                Math.Max(0.1, memory.TaskRelevance);

            var actualRecallSignal =
                1.0 + Math.Log(1 + Math.Max(0, memory.AccessCount));

            var recallRatio = actualRecallSignal / expectedRecallPressure;

            var score = initialScore * recallRatio;

            return Clamp(score);
        }

        private static double Clamp(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            return Math.Max(0, Math.Min(1, value));
        }
    }
}