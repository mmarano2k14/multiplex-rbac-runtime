using System;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;

namespace Multiplexed.AI.Runtime.AI.Rag.Retrieval
{
    /// <summary>
    /// Default deterministic implementation of <see cref="IRagBatchMerger"/>.
    ///
    /// PURPOSE:
    /// - Merges multiple retrieval batches into a single deterministic batch.
    /// - Supports expert DAG mode where retrieval is split across multiple steps.
    /// - Applies merge, deduplication, ranking, and stable ordering consistently.
    ///
    /// DESIGN:
    /// - This class is purely focused on batch aggregation.
    /// - It does not execute providers.
    /// - It does not contain domain-specific logic.
    ///
    /// IMPORTANT:
    /// - Same input batches must always produce the same output batch.
    /// - Ordering and tie-breaking must remain stable.
    /// </summary>
    public sealed class DefaultRagBatchMerger : IRagBatchMerger
    {
        private readonly RagMultiProviderRetrievalOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRagBatchMerger"/> class.
        /// </summary>
        /// <param name="options">
        /// The merge options reused from the multi-provider retrieval model.
        /// </param>
        public DefaultRagBatchMerger(RagMultiProviderRetrievalOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Merges the provided retrieval batches into a single deterministic batch.
        ///
        /// FLOW:
        /// 1. Flatten all items
        /// 2. Apply merge strategy
        /// 3. Apply deduplication
        /// 4. Apply ranking
        /// 5. Reassign stable order
        /// 6. Build aggregate diagnostics
        ///
        /// IMPORTANT:
        /// - No randomness is allowed.
        /// - Batch order is normalized before merge.
        /// </summary>
        /// <param name="batches">
        /// The retrieval batches to merge.
        /// </param>
        /// <returns>
        /// A single deterministic retrieval batch.
        /// </returns>
        public RagRetrievalBatch Merge(IReadOnlyList<RagRetrievalBatch> batches)
        {
            ArgumentNullException.ThrowIfNull(batches);

            var normalizedBatches = batches
                .Where(x => x is not null)
                .OrderBy(x => GetSafeItemCount(x))
                .ThenBy(BuildBatchOrderingKey, StringComparer.Ordinal)
                .ToArray();

            var rawItems = normalizedBatches
                .SelectMany(x => x.Items ?? Array.Empty<RagNormalizedItem>())
                .Where(x => x is not null)
                .ToArray();

            var mergedItems = ApplyMergeMode(rawItems);
            var deduplicatedItems = ApplyDeduplication(mergedItems);
            var rankedItems = ApplyRanking(deduplicatedItems);
            var stabilizedItems = ReassignStableOrder(rankedItems);

            ValidateStableOrder(stabilizedItems);

            var diagnostics = BuildDiagnostics(
                normalizedBatches,
                rawItems.Length,
                mergedItems.Count,
                deduplicatedItems.Count,
                stabilizedItems.Count);

            return new RagRetrievalBatch
            {
                Items = stabilizedItems,
                Diagnostics = diagnostics
            };
        }

        #region Merge Pipeline

        /// <summary>
        /// Applies the configured merge strategy to the raw flattened item list.
        ///
        /// PURPOSE:
        /// - Defines how items from multiple batches enter the common pipeline.
        /// - Keeps merge semantics explicit and configurable.
        /// </summary>
        private IReadOnlyList<RagNormalizedItem> ApplyMergeMode(IReadOnlyList<RagNormalizedItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            return _options.MergeMode switch
            {
                RagMergeMode.Concat => OrderDeterministically(items),

                RagMergeMode.StableUnion => OrderDeterministically(items),

                RagMergeMode.BestScoreWins => items
                    .OrderByDescending(x => x.Score ?? double.MinValue)
                    .ThenBy(x => x.ProviderKey, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceType)
                    .ThenBy(x => x.Id, StringComparer.Ordinal)
                    .ToArray(),

                RagMergeMode.Unknown => throw new InvalidOperationException(
                    "RAG merge mode cannot be Unknown."),

                _ => throw new NotSupportedException(
                    $"RAG merge mode '{_options.MergeMode}' is not supported.")
            };
        }

        /// <summary>
        /// Applies the configured deduplication strategy.
        ///
        /// PURPOSE:
        /// - Removes duplicate items produced across multiple retrieval sources.
        /// - Preserves deterministic preferred-item selection.
        /// </summary>
        private IReadOnlyList<RagNormalizedItem> ApplyDeduplication(IReadOnlyList<RagNormalizedItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            return _options.DeduplicationMode switch
            {
                RagDeduplicationMode.None => items,

                RagDeduplicationMode.ById => items
                    .GroupBy(x => x.Id ?? string.Empty, StringComparer.Ordinal)
                    .Select(SelectPreferredItem)
                    .ToArray(),

                RagDeduplicationMode.BySourceAndId => items
                    .GroupBy(x => $"{x.ProviderKey ?? string.Empty}::{x.Id ?? string.Empty}", StringComparer.Ordinal)
                    .Select(SelectPreferredItem)
                    .ToArray(),

                RagDeduplicationMode.Unknown => throw new InvalidOperationException(
                    "RAG deduplication mode cannot be Unknown."),

                _ => throw new NotSupportedException(
                    $"RAG deduplication mode '{_options.DeduplicationMode}' is not supported.")
            };
        }

        /// <summary>
        /// Applies the configured ranking strategy.
        ///
        /// PURPOSE:
        /// - Defines final priority before stable order is reassigned.
        /// - Keeps ranking behavior explicit and deterministic.
        /// </summary>
        private IReadOnlyList<RagNormalizedItem> ApplyRanking(IReadOnlyList<RagNormalizedItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            return _options.RankingMode switch
            {
                RagRankingMode.None => OrderDeterministically(items),

                RagRankingMode.ScoreDescending => items
                    .OrderByDescending(x => x.Score ?? double.MinValue)
                    .ThenBy(x => x.ProviderKey, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceType)
                    .ThenBy(x => x.Id, StringComparer.Ordinal)
                    .ToArray(),

                RagRankingMode.DeterministicScoreThenId => items
                    .OrderByDescending(x => x.Score ?? double.MinValue)
                    .ThenBy(x => x.ProviderKey, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceType)
                    .ThenBy(x => x.Id, StringComparer.Ordinal)
                    .ToArray(),

                RagRankingMode.Unknown => throw new InvalidOperationException(
                    "RAG ranking mode cannot be Unknown."),

                _ => throw new NotSupportedException(
                    $"RAG ranking mode '{_options.RankingMode}' is not supported.")
            };
        }

        /// <summary>
        /// Reassigns dense sequential stable order values.
        ///
        /// PURPOSE:
        /// - Ensures the final merged list is replay-safe and composition-friendly.
        /// - Makes downstream fragment generation deterministic.
        /// </summary>
        private static IReadOnlyList<RagNormalizedItem> ReassignStableOrder(
            IReadOnlyList<RagNormalizedItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            var result = new RagNormalizedItem[items.Count];

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i] ?? throw new InvalidOperationException(
                    $"RAG merge produced a null item at index {i}.");

                result[i] = new RagNormalizedItem
                {
                    Id = item.Id,
                    ProviderKey = item.ProviderKey,
                    ProviderKind = item.ProviderKind,
                    SourceType = item.SourceType,
                    RetrievalKey = item.RetrievalKey,
                    RetrievalKind = item.RetrievalKind,
                    ContentType = item.ContentType,
                    ContentText = item.ContentText,
                    Score = item.Score,
                    Payload = item.Payload,
                    StableOrder = i,
                    Metadata = item.Metadata
                };
            }

            return result;
        }

        #endregion

        #region Deterministic Helpers

        /// <summary>
        /// Applies the default deterministic ordering used across merge flows.
        ///
        /// RULES:
        /// - ProviderKey
        /// - SourceType
        /// - Id
        /// </summary>
        private static IReadOnlyList<RagNormalizedItem> OrderDeterministically(
            IEnumerable<RagNormalizedItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            return items
                .Where(x => x is not null)
                .OrderBy(x => x.ProviderKey ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.SourceType)
                .ThenBy(x => x.Id ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Selects the preferred item from a duplicate group using deterministic rules.
        ///
        /// RULES:
        /// - highest score wins
        /// - then provider key
        /// - then source type
        /// - then item id
        /// </summary>
        private static RagNormalizedItem SelectPreferredItem(
            IGrouping<string, RagNormalizedItem> group)
        {
            ArgumentNullException.ThrowIfNull(group);

            return group
                .Where(x => x is not null)
                .OrderByDescending(x => x.Score ?? double.MinValue)
                .ThenBy(x => x.ProviderKey ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.SourceType)
                .ThenBy(x => x.Id ?? string.Empty, StringComparer.Ordinal)
                .First();
        }

        /// <summary>
        /// Builds a deterministic ordering key for an input batch.
        ///
        /// PURPOSE:
        /// - Stabilizes outer batch ordering before flattening.
        /// - Prevents caller-provided order from influencing final results.
        /// </summary>
        private static string BuildBatchOrderingKey(RagRetrievalBatch batch)
        {
            ArgumentNullException.ThrowIfNull(batch);

            return string.Join(
                "|",
                (batch.Items ?? Array.Empty<RagNormalizedItem>())
                    .Where(x => x is not null)
                    .OrderBy(x => x.ProviderKey ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(x => x.Id ?? string.Empty, StringComparer.Ordinal)
                    .Select(x => $"{x.ProviderKey ?? string.Empty}:{x.Id ?? string.Empty}"));
        }

        private static int GetSafeItemCount(RagRetrievalBatch batch)
        {
            ArgumentNullException.ThrowIfNull(batch);
            return batch.Items?.Count ?? 0;
        }

        private static void ValidateStableOrder(IReadOnlyList<RagNormalizedItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i] ?? throw new InvalidOperationException(
                    $"RAG merge produced a null stabilized item at index {i}.");

                if (item.StableOrder != i)
                {
                    throw new InvalidOperationException(
                        $"RAG merge produced a non-dense StableOrder sequence at index {i}. Expected {i}, found {item.StableOrder}.");
                }
            }
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Builds aggregate diagnostics for the merged retrieval batch.
        ///
        /// PURPOSE:
        /// - Exposes the transformation counts across the merge pipeline.
        /// - Preserves provider diagnostics when available.
        /// </summary>
        private static RagRetrievalDiagnostics BuildDiagnostics(
            IReadOnlyList<RagRetrievalBatch> batches,
            int rawItemCount,
            int afterMergeCount,
            int afterDedupCount,
            int finalItemCount)
        {
            ArgumentNullException.ThrowIfNull(batches);

            var providerDiagnostics = batches
                .Where(x => x?.Diagnostics?.Providers is not null)
                .SelectMany(x => x!.Diagnostics!.Providers)
                .Where(x => x is not null)
                .OrderBy(x => x.ProviderKey ?? string.Empty, StringComparer.Ordinal)
                .ToArray();

            return new RagRetrievalDiagnostics
            {
                TotalProviders = providerDiagnostics.Length,
                SuccessfulProviders = providerDiagnostics.Count(x => x.Success),
                FailedProviders = providerDiagnostics.Count(x => !x.Success),
                RawItemCount = rawItemCount,
                AfterMergeCount = afterMergeCount,
                AfterDedupCount = afterDedupCount,
                FinalItemCount = finalItemCount,
                TotalDurationMs = providerDiagnostics.Sum(x => x.DurationMs),
                Providers = providerDiagnostics
            };
        }

        #endregion
    }
}