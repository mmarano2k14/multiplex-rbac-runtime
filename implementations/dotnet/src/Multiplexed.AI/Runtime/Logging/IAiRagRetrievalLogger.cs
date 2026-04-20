using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Emits structured runtime events for RAG retrieval execution.
    ///
    /// PURPOSE:
    /// - Tracks retrieval orchestration at the provider and merge level.
    /// - Complements step-level logging with internal retrieval visibility.
    /// - Supports realtime observability for debugging and diagnostics.
    /// </summary>
    public interface IAiRagRetrievalLogger
    {
        void RetrievalStarted(string? executionId, string retrievalName, int providerCount);

        void ProviderStarted(string? executionId, string retrievalName, string providerKey);

        void ProviderCompleted(
            string? executionId,
            string retrievalName,
            string providerKey,
            int itemCount,
            long durationMs);

        void ProviderFailed(
            string? executionId,
            string retrievalName,
            string providerKey,
            long durationMs,
            Exception exception);

        void MergeCompleted(
            string? executionId,
            string retrievalName,
            int rawItemCount,
            int finalItemCount,
            long durationMs);

        void RetrievalCompleted(
            string? executionId,
            string retrievalName,
            RagRetrievalBatch batch,
            long durationMs);
    }
}