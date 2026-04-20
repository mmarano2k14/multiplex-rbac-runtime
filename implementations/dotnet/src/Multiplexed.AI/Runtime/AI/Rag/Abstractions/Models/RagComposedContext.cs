using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Represents the final context produced by a RAG composer.
    ///
    /// PURPOSE:
    /// - Encapsulates the strongly typed context generated from a retrieval batch.
    /// - Preserves the normalized fragments that contributed to the final result.
    /// - Provides a stable output model between retrieval/composition and prompt layers.
    ///
    /// DESIGN:
    /// - <typeparamref name="TContext"/> contains the final structured context
    ///   intended for prompt execution or downstream processing.
    /// - <see cref="Fragments"/> preserves the ordered context fragments that were
    ///   used to build that final context.
    /// - <see cref="Metadata"/> allows composers to attach non-domain-specific
    ///   composition information such as counts, diagnostics, or orchestration hints.
    ///
    /// USAGE:
    /// - Returned by implementations of <c>IRagComposer&lt;TContext&gt;</c>.
    /// - Consumed by prompt layers or downstream runtime steps.
    /// - Useful for keeping both a structured context and its supporting fragments.
    /// </summary>
    /// <typeparam name="TContext">
    /// The final strongly typed context produced by the composer.
    /// </typeparam>
    public sealed class RagComposedContext<TContext>
    {
        /// <summary>
        /// Gets or initializes the final strongly typed context produced by composition.
        ///
        /// This is the main payload that downstream prompt or execution layers
        /// are expected to consume.
        /// </summary>
        public TContext? Context { get; init; }

        /// <summary>
        /// Gets or initializes the ordered fragments that contributed to the final context.
        ///
        /// This preserves the composition trail in a normalized and inspectable form.
        /// </summary>
        public IReadOnlyList<RagContextFragment> Fragments { get; init; }
            = Array.Empty<RagContextFragment>();

        /// <summary>
        /// Gets or initializes additional metadata associated with the composition result.
        ///
        /// This can be used for diagnostics, counts, composition hints, or other
        /// non-domain-specific annotations.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Metadata { get; init; }
            = new Dictionary<string, object?>();
    }
}