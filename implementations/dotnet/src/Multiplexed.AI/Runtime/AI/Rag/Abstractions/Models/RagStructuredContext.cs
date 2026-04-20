using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Represents a simple generic structured context produced by a deterministic composer.
    ///
    /// PURPOSE:
    /// - Provides a domain-agnostic context shape for prompt consumption.
    /// - Keeps the first V1 composer generic and reusable.
    /// - Preserves both a flattened text view and grouped fragments.
    ///
    /// DESIGN:
    /// - This model is intentionally generic.
    /// - It does not assume CV/job, incident, document, or any domain-specific shape.
    /// - It is suitable as a default prompt-ready context model.
    /// </summary>
    public sealed class RagStructuredContext
    {
        /// <summary>
        /// Gets or initializes the final flattened context text.
        ///
        /// This value can be directly injected into a prompt template.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the ordered fragment texts.
        ///
        /// This preserves a simpler string-only view of the final context.
        /// </summary>
        public IReadOnlyList<string> OrderedTexts { get; init; }
            = Array.Empty<string>();

        /// <summary>
        /// Gets or initializes grouped fragment texts by logical category.
        ///
        /// This provides a structured representation while remaining domain-agnostic.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Groups { get; init; }
            = new Dictionary<string, IReadOnlyList<string>>();
    }
}