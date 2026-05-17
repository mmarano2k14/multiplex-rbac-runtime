using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionStepResolver"/>.
    ///
    /// PURPOSE:
    /// - Resolve step state from hot <see cref="AiExecutionState.Steps"/> first.
    /// - Resolve archived steps through the external step payload index.
    /// - Load full archived step state from <see cref="IAiStepPayloadStore"/>.
    /// - Avoid repeated Redis/Mongo/payload reads through short-lived local caches.
    ///
    /// PERFORMANCE DESIGN:
    /// - Hot state is always checked first and is never copied into the local cache.
    /// - Local step cache stores loaded archived step states.
    /// - Local index cache stores archived step index entries.
    /// - WarmAsync performs a full archived-index warmup.
    /// - WarmStepsAsync performs incremental warmup for newly evicted steps only.
    ///
    /// IMPORTANT:
    /// - Register this resolver as scoped.
    /// - This class does not mutate <see cref="AiExecutionState"/>.
    /// - This class does not apply retention.
    /// - This class does not decide whether a step should be archived.
    /// </summary>
    public sealed class DefaultAiExecutionStepResolver : IAiExecutionStepResolver
    {
        private static readonly object MissingStepMarker = new();

        private readonly IAiStepPayloadIndexStore _indexStore;
        private readonly IAiStepPayloadStore _stepPayloadStore;

        private readonly ConcurrentDictionary<string, object> _stepCache =
            new(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, AiArchivedStepPayloadIndex> _indexCache =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiExecutionStepResolver"/> class.
        /// </summary>
        public DefaultAiExecutionStepResolver(
            IAiStepPayloadIndexStore indexStore,
            IAiStepPayloadStore stepPayloadStore)
        {
            _indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
            _stepPayloadStore = stepPayloadStore ?? throw new ArgumentNullException(nameof(stepPayloadStore));
        }

        /// <inheritdoc />
        public async Task WarmAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(state);

            var archived = await _indexStore.GetByExecutionAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var entry in archived)
            {
                if (state.Steps.ContainsKey(entry.StepName))
                {
                    continue;
                }

                var cacheKey = BuildCacheKey(executionId, entry.StepName);

                _indexCache[cacheKey] = entry;
                _stepCache.TryRemove(cacheKey, out _);
            }
        }

        /// <inheritdoc />
        public async Task WarmStepsAsync(
            string executionId,
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(stepNames);

            var namesToWarm = stepNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Where(x => !state.Steps.ContainsKey(x))
                .ToArray();

            if (namesToWarm.Length == 0)
            {
                return;
            }

            var entries = await _indexStore.GetManyAsync(
                    executionId,
                    namesToWarm,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var pair in entries)
            {
                var cacheKey = BuildCacheKey(executionId, pair.Key);

                _indexCache[cacheKey] = pair.Value;
                _stepCache.TryRemove(cacheKey, out _);
            }
        }

        /// <inheritdoc />
        public async Task<AiStepState?> GetStepAsync(
            string executionId,
            string stepName,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(state);

            if (state.Steps.TryGetValue(stepName, out var hotStep))
            {
                return hotStep;
            }

            var cacheKey = BuildCacheKey(executionId, stepName);

            if (_stepCache.TryGetValue(cacheKey, out var cachedStep))
            {
                return ReferenceEquals(cachedStep, MissingStepMarker)
                    ? null
                    : (AiStepState)cachedStep;
            }

            if (!_indexCache.TryGetValue(cacheKey, out var archived))
            {
                archived = await _indexStore.GetAsync(
                        executionId,
                        stepName,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (archived is not null)
                {
                    _indexCache[cacheKey] = archived;
                }
            }

            if (archived is null)
            {
                return null;
            }

            var step = await _stepPayloadStore.LoadStepAsync(
                    executionId,
                    stepName,
                    archived.Payload,
                    cancellationToken)
                .ConfigureAwait(false);

            if (step is null)
            {
                return null;
            }

            _stepCache[cacheKey] = step;

            return step;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AiStepState>> GetAllStepsAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(state);

            var result = new List<AiStepState>();

            result.AddRange(state.Steps.Values);

            var archived = await _indexStore.GetByExecutionAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var entry in archived)
            {
                if (state.Steps.ContainsKey(entry.StepName))
                {
                    continue;
                }

                var cacheKey = BuildCacheKey(executionId, entry.StepName);

                _indexCache[cacheKey] = entry;

                var step = await GetStepAsync(
                        executionId,
                        entry.StepName,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (step is not null)
                {
                    result.Add(step);
                }
            }

            return result
                .OrderBy(x => x.StepName, StringComparer.Ordinal)
                .ToList();
        }

        /// <inheritdoc />
        public async Task<AiStepState?> GetStepStatusAsync(
            string executionId,
            string stepName,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(state);

            if (state.Steps.TryGetValue(stepName, out var hotStep))
            {
                return hotStep;
            }

            var cacheKey = BuildCacheKey(executionId, stepName);

            if (_stepCache.TryGetValue(cacheKey, out var cachedStep))
            {
                return ReferenceEquals(cachedStep, MissingStepMarker)
                    ? null
                    : (AiStepState)cachedStep;
            }

            if (!_indexCache.TryGetValue(cacheKey, out var archived))
            {
                archived = await _indexStore.GetAsync(
                        executionId,
                        stepName,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (archived is not null)
                {
                    _indexCache[cacheKey] = archived;
                }
            }

            if (archived is null)
            {
                return null;
            }

            return new AiStepState
            {
                StepName = archived.StepName,
                Status = archived.Status,
                CompletedAtUtc = archived.ArchivedAtUtc,
                UpdatedAtUtc = archived.ArchivedAtUtc
            };
        }

        /// <summary>
        /// Builds a stable local cache key for an execution step.
        /// </summary>
        private static string BuildCacheKey(
            string executionId,
            string stepName)
        {
            return $"{executionId}:{stepName}";
        }
    }
}