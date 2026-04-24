using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.Memory;

namespace Multiplexed.AI.Runtime.Memory
{
    /// <summary>
    /// In-memory consolidated memory store.
    ///
    /// PURPOSE:
    /// - Provides a safe development/test implementation of consolidated memory
    /// - Enables memory lifecycle tests without external dependencies
    ///
    /// IMPORTANT:
    /// - This implementation is process-local
    /// - Data is lost on restart
    /// - Use a durable implementation for production
    /// </summary>
    public sealed class InMemoryAiConsolidatedMemoryStore : IAiConsolidatedMemoryStore
    {
        private readonly ConcurrentDictionary<string, AiConsolidatedMemoryRecord> _items = new();

        public Task SaveAsync(
            AiConsolidatedMemoryRecord memory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(memory);
            ArgumentException.ThrowIfNullOrWhiteSpace(memory.Id);

            memory.UpdatedAtUtc = DateTime.UtcNow;
            _items[memory.Id] = memory;

            return Task.CompletedTask;
        }

        public Task<AiConsolidatedMemoryRecord?> GetAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            _items.TryGetValue(id, out var memory);
            return Task.FromResult(memory);
        }

        public Task<IReadOnlyList<AiConsolidatedMemoryRecord>> SearchAsync(
            string scope,
            string? kind = null,
            int limit = 20,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);

            var take = Math.Max(1, limit);

            var results = _items.Values
                .Where(x => string.Equals(x.Scope, scope, StringComparison.Ordinal))
                .Where(x => kind is null || string.Equals(x.Kind, kind, StringComparison.Ordinal))
                .OrderByDescending(x => x.CurrentScore)
                .ThenByDescending(x => x.UpdatedAtUtc)
                .Take(take)
                .ToList();

            return Task.FromResult<IReadOnlyList<AiConsolidatedMemoryRecord>>(results);
        }

        public Task DeleteAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            _items.TryRemove(id, out _);
            return Task.CompletedTask;
        }
    }
}