using System;
using System.Collections.Generic;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Represents a single fragment of context produced during RAG composition.
    ///
    /// PURPOSE:
    /// - Acts as the atomic unit of context used to build the final composed result.
    /// - Preserves traceability between retrieved items and the final prompt context.
    /// - Allows deterministic ordering and structured grouping of contextual data.
    ///
    /// DESIGN:
    /// - Each fragment is derived from one or more <see cref="RagNormalizedItem"/>.
    /// - Fragments are ordered explicitly using <see cref="Order"/> to guarantee determinism.
    /// - Fragments are flexible and can represent different logical categories
    ///   (facts, entities, runtime data, summaries, etc).
    ///
    /// USAGE:
    /// - Produced by RAG composers during context construction.
    /// - Stored inside <see cref="RagComposedContext{TContext}"/>.
    /// - Can be used for debugging, audit, replay, or prompt visualization.
    /// </summary>
    public sealed class RagContextFragment
    {
        /// <summary>
        /// Gets or initializes the unique fragment key.
        ///
        /// This key should be stable and can be used to identify or group fragments.
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the logical fragment kind.
        ///
        /// This indicates the semantic role of the fragment
        /// (entity, fact, signal, runtime data, etc).
        /// </summary>
        public RagFragmentKind FragmentKind { get; init; }

        /// <summary>
        /// Gets or initializes the textual representation of the fragment.
        ///
        /// This is typically what will be injected into a prompt or used
        /// in context assembly.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the deterministic order of the fragment.
        ///
        /// IMPORTANT:
        /// - This must be stable across executions.
        /// - Used to guarantee deterministic composition.
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// Gets or initializes the optional score associated with the fragment.
        ///
        /// This can be derived from provider scores or computed during composition.
        /// </summary>
        public double? Score { get; init; }

        /// <summary>
        /// Gets or initializes the identifiers of the source items that contributed
        /// to this fragment.
        ///
        /// This ensures traceability between retrieval results and composed context.
        /// </summary>
        public IReadOnlyList<string> SourceIds { get; init; }
            = Array.Empty<string>();

        /// <summary>
        /// Gets or initializes additional metadata associated with the fragment.
        ///
        /// This can include:
        /// - provider info
        /// - grouping tags
        /// - debug data
        /// - formatting hints
        /// </summary>
        public IReadOnlyDictionary<string, object?> Metadata { get; init; }
            = new Dictionary<string, object?>();
    }
}