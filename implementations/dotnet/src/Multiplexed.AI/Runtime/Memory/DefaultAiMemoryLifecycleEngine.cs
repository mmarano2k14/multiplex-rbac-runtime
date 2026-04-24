using Multiplexed.Abstractions.AI.Memory;

namespace Multiplexed.AI.Runtime.Memory
{
    /// <summary>
    /// Default consolidated memory lifecycle engine.
    ///
    /// PURPOSE:
    /// - Applies session-based aging to consolidated memories
    /// - Recomputes scores using deterministic scoring policy
    /// - Reinforces memories when recalled
    /// - Removes memories that fall below pruning threshold
    ///
    /// DESIGN:
    /// - Aging is measured in work sessions, not wall-clock time
    /// - Decay emerges naturally from expected recall pressure
    /// - Recall increases access count and score
    ///
    /// IMPORTANT:
    /// - This engine does not affect execution replay
    /// - This engine does not mutate DAG step state
    /// - This engine only manages consolidated memory records
    /// </summary>
    public sealed class DefaultAiMemoryLifecycleEngine : IAiMemoryLifecycleEngine
    {
        private readonly IAiConsolidatedMemoryStore _store;
        private readonly IAiMemoryScoringPolicy _scoringPolicy;
        private readonly IAiMemoryLifecyclePolicy _lifecyclePolicy;

        public DefaultAiMemoryLifecycleEngine(
            IAiConsolidatedMemoryStore store,
            IAiMemoryScoringPolicy scoringPolicy,
            IAiMemoryLifecyclePolicy lifecyclePolicy)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(scoringPolicy);
            ArgumentNullException.ThrowIfNull(lifecyclePolicy);

            _store = store;
            _scoringPolicy = scoringPolicy;
            _lifecyclePolicy = lifecyclePolicy;
        }

        /// <summary>
        /// Ages all memories in a scope by one work session and recomputes scores.
        ///
        /// SEMANTICS:
        /// - AgeInSessions increments by one
        /// - CurrentScore is recomputed
        /// - No memory is deleted here
        /// - Pruning remains a separate explicit operation
        /// </summary>
        public async Task AgeSessionAsync(
            string scope,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);

            var memories = await _store.SearchAsync(
                scope,
                kind: null,
                limit: int.MaxValue,
                cancellationToken);

            foreach (var memory in memories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                memory.AgeInSessions++;
                memory.CurrentScore = _scoringPolicy.ComputeCurrentScore(memory);
                memory.UpdatedAtUtc = DateTime.UtcNow;

                await _store.SaveAsync(memory, cancellationToken);
            }
        }

        /// <summary>
        /// Reinforces a memory when it is recalled by retrieval or execution.
        ///
        /// SEMANTICS:
        /// - AccessCount increments by one
        /// - CurrentScore is recomputed
        /// - UpdatedAtUtc is refreshed
        /// </summary>
        public async Task ReinforceRecallAsync(
            string memoryId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(memoryId);

            var memory = await _store.GetAsync(memoryId, cancellationToken);

            if (memory is null)
                return;

            memory.AccessCount++;
            memory.CurrentScore = _scoringPolicy.ComputeCurrentScore(memory);
            memory.UpdatedAtUtc = DateTime.UtcNow;

            await _store.SaveAsync(memory, cancellationToken);
        }

        /// <summary>
        /// Prunes memories that no longer satisfy lifecycle thresholds.
        ///
        /// IMPORTANT:
        /// - Pruning applies only to consolidated memory
        /// - It never deletes ledger records or payload artifacts
        /// </summary>
        public async Task PruneAsync(
            string scope,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);

            var memories = await _store.SearchAsync(
                scope,
                kind: null,
                limit: int.MaxValue,
                cancellationToken);

            foreach (var memory in memories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_lifecyclePolicy.ShouldPrune(memory))
                {
                    await _store.DeleteAsync(memory.Id, cancellationToken);
                }
            }
        }
    }
}