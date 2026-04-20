// File: DefaultRagStepResultNormalizer.cs

using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.Abstractions.AI.Rag.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Normalization
{
    /// <summary>
    /// Lightweight step-level RAG result normalizer.
    ///
    /// PURPOSE:
    /// - Used inside rag.retrieval flow
    /// - Ensures result types remain stable (RagRetrievalBatch, etc.)
    /// - Does NOT perform full execution state normalization
    /// </summary>
    public sealed class DefaultRagStepResultNormalizer : IRagStepResultNormalizer
    {
        public object? Normalize(object? value)
        {
            // 🔥 pour l'instant simple (tu peux enrichir plus tard)
            return value;
        }
    }
}