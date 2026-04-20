using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval
{
    /// <summary>
    /// Defines the configurable behavior of a multi-provider retrieval strategy.
    ///
    /// PURPOSE:
    /// - Centralizes orchestration configuration for multi-provider retrieval.
    /// - Keeps the retrieval implementation explicit, testable, and extensible.
    ///
    /// DESIGN:
    /// - Execution mode controls how providers are invoked.
    /// - Fallback policy controls when provider chaining should continue.
    /// - Merge, deduplication, and ranking modes control result shaping.
    /// </summary>
    public sealed class RagMultiProviderRetrievalOptions
    {
        /// <summary>
        /// Gets or initializes the provider execution mode.
        /// </summary>
        public RagProviderExecutionMode ExecutionMode { get; init; }
            = RagProviderExecutionMode.Sequential;

        /// <summary>
        /// Gets or initializes the fallback policy.
        /// </summary>
        public RagFallbackPolicy FallbackPolicy { get; init; }
            = RagFallbackPolicy.None;

        /// <summary>
        /// Gets or initializes the merge mode.
        /// </summary>
        public RagMergeMode MergeMode { get; init; }
            = RagMergeMode.StableUnion;

        /// <summary>
        /// Gets or initializes the ranking mode.
        /// </summary>
        public RagRankingMode RankingMode { get; init; }
            = RagRankingMode.DeterministicScoreThenId;

        /// <summary>
        /// Gets or initializes the deduplication mode.
        /// </summary>
        public RagDeduplicationMode DeduplicationMode { get; init; }
            = RagDeduplicationMode.BySourceAndId;

        /// <summary>
        /// Gets or initializes a value indicating whether provider exceptions should
        /// be suppressed and treated as empty results.
        /// </summary>
        public bool SuppressProviderExceptions { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether fallback execution should
        /// stop as soon as a provider returns one or more items.
        /// </summary>
        public bool StopOnFirstNonEmptyResult { get; init; } = true;
    }
}