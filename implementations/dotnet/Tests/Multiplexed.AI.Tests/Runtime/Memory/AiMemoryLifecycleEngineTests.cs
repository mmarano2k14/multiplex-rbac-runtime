using Multiplexed.Abstractions.AI.Memory;
using Multiplexed.AI.Runtime.Memory;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Memory
{
    /// <summary>
    /// Unit tests for <see cref="DefaultAiMemoryLifecycleEngine"/>.
    ///
    /// PURPOSE:
    /// - Validate session-based aging (AgeInSessions + score recompute)
    /// - Validate recall reinforcement (AccessCount + score recompute)
    /// - Validate pruning decisions via lifecycle policy
    ///
    /// IMPORTANT:
    /// - No DAG / execution state involved
    /// - Operates only on consolidated memory store
    /// </summary>
    public sealed class AiMemoryLifecycleEngineTests
    {
        [Fact]
        public async Task AgeSessionAsync_Should_Increment_Age_And_Recompute_Score()
        {
            // Arrange
            var store = new InMemoryAiConsolidatedMemoryStore();
            var scoring = new DefaultAiMemoryScoringPolicy();
            var lifecycle = new DefaultAiMemoryLifecyclePolicy();
            var engine = new DefaultAiMemoryLifecycleEngine(store, scoring, lifecycle);

            var memory = CreateMemory(scope: "test",
                relevance: 0.8, novelty: 0.6, confidence: 0.7);

            await store.SaveAsync(memory);

            var beforeAge = memory.AgeInSessions;
            var beforeScore = memory.CurrentScore;

            // Act
            await engine.AgeSessionAsync("test");

            var updated = await store.GetAsync(memory.Id);

            // Assert
            Assert.NotNull(updated);
            Assert.Equal(beforeAge + 1, updated!.AgeInSessions);
            Assert.NotEqual(beforeScore, updated.CurrentScore);
        }

        [Fact]
        public async Task ReinforceRecallAsync_Should_Increment_Access_And_Recompute_Score()
        {
            // Arrange
            var store = new InMemoryAiConsolidatedMemoryStore();
            var scoring = new DefaultAiMemoryScoringPolicy();
            var lifecycle = new DefaultAiMemoryLifecyclePolicy();
            var engine = new DefaultAiMemoryLifecycleEngine(store, scoring, lifecycle);

            var memory = CreateMemory(scope: "test",
                relevance: 0.9, novelty: 0.4, confidence: 0.8);

            await store.SaveAsync(memory);

            var beforeAccess = memory.AccessCount;
            var beforeScore = memory.CurrentScore;

            // Act
            await engine.ReinforceRecallAsync(memory.Id);

            var updated = await store.GetAsync(memory.Id);

            // Assert
            Assert.NotNull(updated);
            Assert.Equal(beforeAccess + 1, updated!.AccessCount);
            Assert.NotEqual(beforeScore, updated.CurrentScore);
        }

        [Fact]
        public async Task PruneAsync_Should_Delete_Memories_Below_Threshold()
        {
            // Arrange
            var store = new InMemoryAiConsolidatedMemoryStore();
            var scoring = new DefaultAiMemoryScoringPolicy();
            var lifecycle = new DefaultAiMemoryLifecyclePolicy();
            var engine = new DefaultAiMemoryLifecycleEngine(store, scoring, lifecycle);

            // Memory designed to be pruned:
            // - very low score
            // - no access
            // - aged
            var prunable = CreateMemory(scope: "test",
                relevance: 0.1, novelty: 0.1, confidence: 0.1);

            prunable.AccessCount = 0;
            prunable.AgeInSessions = 2;
            prunable.InitialScore = 0.05;
            prunable.CurrentScore = 0.02;

            // Memory that should remain
            var keeper = CreateMemory(scope: "test",
                relevance: 0.9, novelty: 0.7, confidence: 0.8);

            await store.SaveAsync(prunable);
            await store.SaveAsync(keeper);

            // Act
            await engine.PruneAsync("test");

            var pruned = await store.GetAsync(prunable.Id);
            var kept = await store.GetAsync(keeper.Id);

            // Assert
            Assert.Null(pruned);
            Assert.NotNull(kept);
        }

        // ---------------------------------------------------------------------
        // TEST HELPER
        // ---------------------------------------------------------------------

        /// <summary>
        /// Creates a memory with deterministic initial/current scores.
        /// </summary>
        private static AiConsolidatedMemoryRecord CreateMemory(
            string scope,
            double relevance,
            double novelty,
            double confidence)
        {
            var scoring = new DefaultAiMemoryScoringPolicy();

            var memory = new AiConsolidatedMemoryRecord
            {
                Scope = scope,
                Kind = "semantic",
                Content = "test",
                TaskRelevance = relevance,
                Novelty = novelty,
                Confidence = confidence,
                AccessCount = 0,
                AgeInSessions = 0
            };

            memory.InitialScore = scoring.ComputeInitialScore(memory);
            memory.CurrentScore = memory.InitialScore;

            return memory;
        }
    }
}