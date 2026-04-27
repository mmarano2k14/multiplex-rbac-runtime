using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Redis-cached archived step payload index store.
    ///
    /// PURPOSE:
    /// - Uses MongoDB as the durable source of truth.
    /// - Uses Redis as a fast lookup cache for archived step index entries.
    /// - Keeps step lookup fast after retention eviction.
    ///
    /// DESIGN:
    /// - MarkArchivedAsync is write-through:
    ///   Mongo first, then Redis cache.
    /// - GetAsync reads Redis first, then falls back to Mongo.
    /// - GetManyAsync performs batch Redis lookup first, then Mongo fallback for misses.
    /// - DeleteAsync removes from Mongo and Redis.
    ///
    /// IMPORTANT:
    /// - This class is an index store decorator.
    /// - It must not contain Redis key-building logic directly.
    /// - Redis details belong to IAiStepPayloadIndexCache.
    /// - Mongo remains authoritative.
    /// </summary>
    public sealed class CachedAiStepPayloadIndexStore : IAiStepPayloadIndexStore
    {
        private readonly MongoAiStepPayloadIndexStore _mongo;
        private readonly IAiStepPayloadIndexCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedAiStepPayloadIndexStore"/> class.
        /// </summary>
        public CachedAiStepPayloadIndexStore(
            MongoAiStepPayloadIndexStore mongo,
            IAiStepPayloadIndexCache cache)
        {
            _mongo = mongo ?? throw new ArgumentNullException(nameof(mongo));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <inheritdoc />
        public async Task MarkArchivedAsync(
            AiArchivedStepPayloadIndex entry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);

            await _mongo.MarkArchivedAsync(
                    entry,
                    cancellationToken)
                .ConfigureAwait(false);

            await _cache.SetAsync(
                    entry,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AiArchivedStepPayloadIndex?> GetAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            var cached = await _cache.GetAsync(
                    executionId,
                    stepName,
                    cancellationToken)
                .ConfigureAwait(false);

            if (cached is not null)
            {
                return cached;
            }

            var entry = await _mongo.GetAsync(
                    executionId,
                    stepName,
                    cancellationToken)
                .ConfigureAwait(false);

            if (entry is not null)
            {
                await _cache.SetAsync(
                        entry,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return entry;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            return _mongo.GetByExecutionAsync(
                executionId,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
            string executionId,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(stepNames);

            var requestedStepNames = stepNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (requestedStepNames.Length == 0)
            {
                return new Dictionary<string, AiArchivedStepPayloadIndex>(StringComparer.Ordinal);
            }

            var cached = await _cache.GetManyAsync(
                    executionId,
                    requestedStepNames,
                    cancellationToken)
                .ConfigureAwait(false);

            var missingStepNames = requestedStepNames
                .Where(x => !cached.ContainsKey(x))
                .ToArray();

            if (missingStepNames.Length == 0)
            {
                return cached;
            }

            var mongoEntries = await _mongo.GetManyAsync(
                    executionId,
                    missingStepNames,
                    cancellationToken)
                .ConfigureAwait(false);

            if (mongoEntries.Count > 0)
            {
                await _cache.SetManyAsync(
                        mongoEntries.Values.ToArray(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var result = new Dictionary<string, AiArchivedStepPayloadIndex>(
                cached,
                StringComparer.Ordinal);

            foreach (var pair in mongoEntries)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            await _mongo.DeleteAsync(
                    executionId,
                    stepName,
                    cancellationToken)
                .ConfigureAwait(false);

            await _cache.DeleteAsync(
                    executionId,
                    stepName,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}