using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Composition;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.AI.Rag.Composition.Deterministic
{
    /// <summary>
    /// Builds a deterministic composed context from normalized retrieval items.
    ///
    /// PURPOSE:
    /// - Transforms retrieval output into ordered and traceable context fragments.
    /// - Produces a stable prompt-ready context.
    /// - Preserves replay and debugging guarantees by enforcing deterministic ordering.
    ///
    /// DESIGN:
    /// - Input is always <see cref="RagRetrievalBatch"/>.
    /// - Output is a generic <see cref="RagStructuredContext"/>.
    /// - The composer is intentionally domain-agnostic.
    ///
    /// IMPORTANT:
    /// - Same input must produce the same fragments and same final context text.
    /// - No non-deterministic ordering is allowed.
    /// - Fragment generation must remain stable and inspectable.
    /// </summary>
    public sealed class DeterministicComposer : IRagComposer<RagStructuredContext>
    {
        private readonly IAiRagCompositionLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeterministicComposer"/> class.
        /// </summary>
        /// <param name="logger">
        /// The composition logger used for realtime observability.
        /// </param>
        public DeterministicComposer(IAiRagCompositionLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Builds a deterministic composed context from a retrieval batch.
        ///
        /// FLOW:
        /// 1. Extract execution identifier
        /// 2. Log composition start
        /// 3. Normalize and order items deterministically
        /// 4. Convert items to context fragments
        /// 5. Build grouped and flattened text representations
        /// 6. Log composition completion
        ///
        /// IMPORTANT:
        /// - Fragment count and order must remain stable.
        /// - Empty or whitespace-only texts are ignored.
        /// </summary>
        /// <param name="batch">
        /// The retrieval batch produced by a retrieval strategy.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A composed context containing deterministic fragments and a prompt-ready
        /// structured context.
        /// </returns>
        public Task<RagComposedContext<RagStructuredContext>> ComposeAsync(
            RagRetrievalBatch batch,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(batch);

            var start = DateTime.UtcNow;
            var executionId = TryGetExecutionId(batch);
            const string composerName = "deterministic";

            _logger.CompositionStarted(
                executionId,
                composerName,
                batch.Items.Count);

            try
            {
                // 1. Normalize ordering before fragment generation.
                var orderedItems = OrderDeterministically(batch.Items);

                // 2. Build deterministic fragments.
                var fragments = BuildFragments(orderedItems);

                // 3. Build grouped text structure.
                var groups = BuildGroups(fragments);

                // 4. Build flattened ordered texts.
                var orderedTexts = fragments
                    .OrderBy(x => x.Order)
                    .Select(x => x.Text)
                    .ToArray();

                // 5. Build final flattened text block.
                var finalText = BuildFinalText(orderedTexts);

                var context = new RagStructuredContext
                {
                    Text = finalText,
                    OrderedTexts = orderedTexts,
                    Groups = groups
                };

                var result = new RagComposedContext<RagStructuredContext>
                {
                    Context = context,
                    Fragments = fragments,
                    Metadata = BuildMetadata(batch, fragments, start)
                };

                _logger.CompositionCompleted(
                    executionId,
                    composerName,
                    fragments.Count,
                    GetDuration(start));

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.CompositionFailed(
                    executionId,
                    composerName,
                    GetDuration(start),
                    ex);

                throw;
            }
        }

        #region Fragment Construction

        /// <summary>
        /// Applies deterministic ordering to retrieval items before composition.
        ///
        /// RULES:
        /// - StableOrder first
        /// - ProviderKey second
        /// - Id third
        ///
        /// PURPOSE:
        /// - Preserves prior retrieval ordering when available.
        /// - Guarantees stable fragment construction when prior stable order exists.
        /// </summary>
        private static IReadOnlyList<RagNormalizedItem> OrderDeterministically(
            IReadOnlyList<RagNormalizedItem> items)
        {
            return items
                .OrderBy(x => x.StableOrder)
                .ThenBy(x => x.ProviderKey, StringComparer.Ordinal)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Converts normalized items into deterministic context fragments.
        ///
        /// PURPOSE:
        /// - Creates traceable fragment objects from retrieval items.
        /// - Preserves provider and source lineage through metadata.
        ///
        /// IMPORTANT:
        /// - Items with no usable text are ignored.
        /// - Fragment order is assigned sequentially and deterministically.
        /// </summary>
        private static IReadOnlyList<RagContextFragment> BuildFragments(
            IReadOnlyList<RagNormalizedItem> items)
        {
            var fragments = new List<RagContextFragment>(items.Count);
            var order = 0;

            foreach (var item in items)
            {
                var text = NormalizeText(item.ContentText);

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                fragments.Add(new RagContextFragment
                {
                    Key = $"{item.ProviderKey}:{item.Id}",
                    FragmentKind = MapFragmentKind(item),
                    Text = text,
                    Order = order++,
                    Score = item.Score,
                    SourceIds = new[] { item.Id },
                    Metadata = new Dictionary<string, object?>
                    {
                        ["ProviderKey"] = item.ProviderKey,
                        ["ProviderKind"] = item.ProviderKind.ToString(),
                        ["SourceType"] = item.SourceType.ToString(),
                        ["RetrievalKey"] = item.RetrievalKey,
                        ["RetrievalKind"] = item.RetrievalKind.ToString(),
                        ["ContentType"] = item.ContentType
                    }
                });
            }

            return fragments;
        }

        /// <summary>
        /// Maps a normalized item to a fragment kind.
        ///
        /// PURPOSE:
        /// - Provides a stable semantic classification for composition.
        /// - Keeps the mapping generic and domain-agnostic.
        /// </summary>
        private static RagFragmentKind MapFragmentKind(RagNormalizedItem item)
        {
            if (item.ProviderKind == RagProviderKind.Runtime)
            {
                return RagFragmentKind.Runtime;
            }

            if (!string.IsNullOrWhiteSpace(item.ContentType) &&
                item.ContentType.Contains("summary", StringComparison.OrdinalIgnoreCase))
            {
                return RagFragmentKind.Summary;
            }

            return RagFragmentKind.Fact;
        }

        #endregion

        #region Structured Context Construction

        /// <summary>
        /// Builds grouped fragment text collections.
        ///
        /// PURPOSE:
        /// - Creates a structured, domain-agnostic grouped view of fragment text.
        /// - Keeps ordering stable inside each group.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildGroups(
            IReadOnlyList<RagContextFragment> fragments)
        {
            return fragments
                .GroupBy(x => x.FragmentKind.ToString(), StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .OrderBy(x => x.Order)
                        .Select(x => x.Text)
                        .ToArray(),
                    StringComparer.Ordinal);
        }

        /// <summary>
        /// Builds the final flattened prompt-ready text.
        ///
        /// PURPOSE:
        /// - Produces a simple, deterministic text payload for prompt injection.
        /// - Preserves fragment order exactly.
        /// </summary>
        private static string BuildFinalText(IReadOnlyList<string> orderedTexts)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < orderedTexts.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.Append(orderedTexts[i]);
            }

            return builder.ToString();
        }

        #endregion

        #region Metadata

        /// <summary>
        /// Builds composition metadata.
        ///
        /// PURPOSE:
        /// - Preserves useful composition diagnostics.
        /// - Exposes counts and duration for observability.
        /// </summary>
        private static IReadOnlyDictionary<string, object?> BuildMetadata(
            RagRetrievalBatch batch,
            IReadOnlyList<RagContextFragment> fragments,
            DateTime start)
        {
            return new Dictionary<string, object?>
            {
                ["InputItemCount"] = batch.Items.Count,
                ["FragmentCount"] = fragments.Count,
                ["DurationMs"] = GetDuration(start)
            };
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Normalizes raw text before fragment creation.
        ///
        /// PURPOSE:
        /// - Trims whitespace.
        /// - Ensures deterministic text cleanup.
        /// </summary>
        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        /// <summary>
        /// Calculates elapsed duration in milliseconds.
        /// </summary>
        private static long GetDuration(DateTime start)
        {
            return (long)(DateTime.UtcNow - start).TotalMilliseconds;
        }

        /// <summary>
        /// Attempts to extract an execution identifier from retrieval diagnostics.
        ///
        /// PURPOSE:
        /// - Aligns composition logs with retrieval and DAG execution logs.
        /// - Safe: returns null when no execution identifier is available.
        /// </summary>
        private static string? TryGetExecutionId(RagRetrievalBatch batch)
        {
            // V1 note:
            // RetrievalBatch currently does not expose a first-class execution id field.
            // We try to recover it from diagnostics if the pipeline later enriches it.
            return null;
        }

        #endregion
    }
}