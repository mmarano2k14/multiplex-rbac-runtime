using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.AI.Rag.Retrieval.MultiProvider
{
    /// <summary>
    /// Coordinates multiple RAG providers and produces a single deterministic retrieval result.
    ///
    /// PURPOSE:
    /// - Orchestrates multiple providers using configurable execution strategies.
    /// - Applies merge, deduplication, ranking, and stable ordering.
    /// - Produces diagnostics for observability and debugging.
    ///
    /// DESIGN:
    /// - Providers are responsible for data access and normalization.
    /// - This class is responsible for orchestration and aggregation only.
    ///
    /// IMPORTANT:
    /// - Deterministic output is mandatory.
    /// - Same input MUST produce the same output.
    /// - Ordering rules must be stable and reproducible.
    /// </summary>
    public sealed class MultiProviderRetrieval : IRagRetrieval
    {
        private readonly IReadOnlyList<INormalizingRagProvider> _providers;
        private readonly RagMultiProviderRetrievalOptions _options;
        private readonly IAiRagRetrievalLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiProviderRetrieval"/> class.
        ///
        /// PURPOSE:
        /// - Validates and normalizes provider collection.
        /// - Ensures deterministic provider ordering.
        ///
        /// IMPORTANT:
        /// - Provider order is normalized using ordinal sorting to guarantee stability.
        /// </summary>
        public MultiProviderRetrieval(
            IEnumerable<INormalizingRagProvider> providers,
            RagMultiProviderRetrievalOptions options,
            IAiRagRetrievalLogger logger)
        {
            ArgumentNullException.ThrowIfNull(providers);
            ArgumentNullException.ThrowIfNull(options);

            _providers = providers
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToArray();

            _options = options;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the multi-provider retrieval pipeline.
        ///
        /// FLOW:
        /// 1. Execute each provider (with diagnostics)
        /// 2. Collect all batches
        /// 3. Merge results deterministically
        /// 4. Build diagnostics
        /// 5. Return final batch
        ///
        /// IMPORTANT:
        /// - This method is the orchestration entry point.
        /// - Must remain deterministic across executions.
        /// </summary>
        public async Task<RagRetrievalBatch> RetrieveAsync(
            RagExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var startTotal = DateTime.UtcNow;

            // Extract executionId if available (safe, non-breaking)
            var executionId = TryGetExecutionId(context);

            const string retrievalName = "multi-provider";

            // 🔥 START LOG
            _logger.RetrievalStarted(executionId, retrievalName, _providers.Count);

            var providerResults = new List<(RagRetrievalBatch Batch, RagProviderExecutionDiagnostics Diag)>();

            foreach (var provider in _providers)
            {
                var result = await ExecuteProviderWithDiagnosticsAsync(
                    provider,
                    context,
                    cancellationToken,
                    executionId,
                    retrievalName);

                providerResults.Add(result);
            }

            var batches = providerResults.Select(x => x.Batch).ToList();

            // 🔥 MERGE START TIMER
            var mergeStart = DateTime.UtcNow;

            var merged = MergeDeterministic(batches);

            // 🔥 MERGE LOG
            _logger.MergeCompleted(
                executionId,
                retrievalName,
                rawItemCount: providerResults.Sum(x => x.Batch.Items.Count),
                finalItemCount: merged.Items.Count,
                durationMs: GetDuration(mergeStart));

            var diagnostics = BuildDiagnostics(
                providerResults,
                merged,
                startTotal);

            var finalBatch = new RagRetrievalBatch
            {
                Items = merged.Items,
                Diagnostics = diagnostics
            };

            // 🔥 END LOG
            _logger.RetrievalCompleted(
                executionId,
                retrievalName,
                finalBatch,
                GetDuration(startTotal));

            return finalBatch;
        }

        #region Provider Execution

        /// <summary>
        /// Executes a single provider and captures execution diagnostics.
        ///
        /// PURPOSE:
        /// - Wraps provider execution in a safe and observable layer.
        /// - Ensures a non-null batch is always returned.
        ///
        /// FLOW:
        /// 1. Start timer
        /// 2. Execute provider
        /// 3. Capture result or exception
        /// 4. Build diagnostics object
        ///
        /// IMPORTANT:
        /// - Exceptions can be suppressed depending on configuration.
        /// - Failure is converted to an empty batch if suppression is enabled.
        /// </summary>
        private async Task<(RagRetrievalBatch, RagProviderExecutionDiagnostics)>
            ExecuteProviderWithDiagnosticsAsync(
                INormalizingRagProvider provider,
                RagExecutionContext context,
                CancellationToken cancellationToken,
                string? executionId,
                string retrievalName)
        {
            var start = DateTime.UtcNow;

            // 🔥 PROVIDER START LOG
            _logger.ProviderStarted(executionId, retrievalName, provider.Key);

            try
            {
                var batch = await provider.RetrieveNormalizedAsync(context, cancellationToken)
                    ?? EmptyBatch();

                var duration = GetDuration(start);

                // 🔥 PROVIDER SUCCESS LOG
                _logger.ProviderCompleted(
                    executionId,
                    retrievalName,
                    provider.Key,
                    batch.Items.Count,
                    duration);

                return (batch, new RagProviderExecutionDiagnostics
                {
                    ProviderKey = provider.Key,
                    Success = true,
                    ItemCount = batch.Items.Count,
                    DurationMs = duration
                });
            }
            catch (Exception ex)
            {
                var duration = GetDuration(start);

                // 🔥 PROVIDER ERROR LOG
                _logger.ProviderFailed(
                    executionId,
                    retrievalName,
                    provider.Key,
                    duration,
                    ex);

                if (!_options.SuppressProviderExceptions)
                    throw;

                return (EmptyBatch(), new RagProviderExecutionDiagnostics
                {
                    ProviderKey = provider.Key,
                    Success = false,
                    Error = ex.Message,
                    DurationMs = duration
                });
            }
        }

        #endregion

        #region Merge Pipeline

        /// <summary>
        /// Merges multiple retrieval batches into a single deterministic result.
        ///
        /// PIPELINE:
        /// 1. Flatten all items
        /// 2. Apply deterministic ordering
        /// 3. Apply deduplication
        /// 4. Apply ranking
        /// 5. Reassign stable order
        ///
        /// IMPORTANT:
        /// - Each stage must preserve determinism.
        /// - No randomness or unstable ordering allowed.
        /// </summary>
        private RagRetrievalBatch MergeDeterministic(IReadOnlyList<RagRetrievalBatch> batches)
        {
            var allItems = batches
                .SelectMany(b => b.Items)
                .ToArray();

            var merged = OrderDeterministically(allItems);

            var dedup = ApplyDeduplication(merged);

            var ranked = ApplyRanking(dedup);

            var stabilized = ReassignStableOrder(ranked);

            return new RagRetrievalBatch { Items = stabilized };
        }

        private IReadOnlyList<RagNormalizedItem> ApplyDeduplication(IReadOnlyList<RagNormalizedItem> items)
        {
            return items
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToArray();
        }

        private IReadOnlyList<RagNormalizedItem> ApplyRanking(IReadOnlyList<RagNormalizedItem> items)
        {
            return items
                .OrderByDescending(x => x.Score ?? 0)
                .ThenBy(x => x.Id)
                .ToArray();
        }

        private static IReadOnlyList<RagNormalizedItem> ReassignStableOrder(IReadOnlyList<RagNormalizedItem> items)
        {
            var result = new RagNormalizedItem[items.Count];

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                result[i] = new RagNormalizedItem
                {
                    Id = item.Id,
                    ContentText = item.ContentText,
                    Score = item.Score,
                    StableOrder = i,
                    ProviderKey = item.ProviderKey,
                    Metadata = item.Metadata
                };
            }

            return result;
        }

        private static IReadOnlyList<RagNormalizedItem> OrderDeterministically(IEnumerable<RagNormalizedItem> items)
        {
            return items
                .OrderBy(x => x.ProviderKey, StringComparer.Ordinal)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToArray();
        }

        #endregion

        #region Diagnostics

        private RagRetrievalDiagnostics BuildDiagnostics(
            IReadOnlyList<(RagRetrievalBatch Batch, RagProviderExecutionDiagnostics Diag)> providerResults,
            RagRetrievalBatch merged,
            DateTime start)
        {
            var list = providerResults.Select(x => x.Diag).ToList();

            return new RagRetrievalDiagnostics
            {
                TotalProviders = list.Count,
                SuccessfulProviders = list.Count(x => x.Success),
                FailedProviders = list.Count(x => !x.Success),

                RawItemCount = providerResults.Sum(x => x.Batch.Items.Count),
                FinalItemCount = merged.Items.Count,

                TotalDurationMs = GetDuration(start),
                Providers = list
            };
        }

        #endregion

        #region Helpers

        private static RagRetrievalBatch EmptyBatch()
        {
            return new RagRetrievalBatch
            {
                Items = Array.Empty<RagNormalizedItem>()
            };
        }

        private static long GetDuration(DateTime start)
        {
            return (long)(DateTime.UtcNow - start).TotalMilliseconds;
        }

        /// <summary>
        /// Attempts to extract the execution identifier from the context.
        ///
        /// PURPOSE:
        /// - Allows correlation with DAG execution logs.
        /// - Safe: does not throw if missing.
        /// </summary>
        private static string? TryGetExecutionId(RagExecutionContext context)
        {
            if (context?.Metadata is null)
                return null;

            return context.Metadata.TryGetValue("ExecutionId", out var value)
                ? value?.ToString()
                : null;
        }

        #endregion
    }
}