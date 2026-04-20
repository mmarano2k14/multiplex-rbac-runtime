using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.Abstractions.AI.Rag.Models;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Emits structured realtime events for RAG retrieval execution.
    ///
    /// PURPOSE:
    /// - Provides internal observability for retrieval orchestration.
    /// - Makes provider-level activity visible in realtime.
    /// - Complements step-level logs with RAG-specific diagnostics.
    /// </summary>
    public sealed class AiRagRetrievalLogger : IAiRagRetrievalLogger
    {
        private readonly IRuntimeEventContext _realtime;

        public AiRagRetrievalLogger(IRuntimeEventContext realtime)
        {
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }

        public void RetrievalStarted(string? executionId, string retrievalName, int providerCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retrievalName);

            _realtime.LogInfo(
                message: $"RAG retrieval '{retrievalName}' started.",
                category: "ai.rag.retrieval.start",
                data: new
                {
                    ExecutionId = executionId,
                    Retrieval = retrievalName,
                    ProviderCount = providerCount
                });
        }

        public void ProviderStarted(string? executionId, string retrievalName, string providerKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retrievalName);
            ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

            _realtime.LogInfo(
                message: $"RAG provider '{providerKey}' started.",
                category: "ai.rag.provider.start",
                data: new
                {
                    ExecutionId = executionId,
                    Retrieval = retrievalName,
                    Provider = providerKey
                });
        }

        public void ProviderCompleted(
            string? executionId,
            string retrievalName,
            string providerKey,
            int itemCount,
            long durationMs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retrievalName);
            ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

            _realtime.LogInfo(
                message: $"RAG provider '{providerKey}' completed.",
                category: "ai.rag.provider.completed",
                data: new
                {
                    ExecutionId = executionId,
                    Retrieval = retrievalName,
                    Provider = providerKey,
                    ItemCount = itemCount,
                    DurationMs = durationMs
                });
        }

        public void ProviderFailed(
            string? executionId,
            string retrievalName,
            string providerKey,
            long durationMs,
            Exception exception)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retrievalName);
            ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
            ArgumentNullException.ThrowIfNull(exception);

            _realtime.LogError(
                message: $"RAG provider '{providerKey}' failed.",
                category: "ai.rag.provider.failed",
                data: new
                {
                    ExecutionId = executionId,
                    Retrieval = retrievalName,
                    Provider = providerKey,
                    DurationMs = durationMs,
                    Exception = exception.Message
                });
        }

        public void MergeCompleted(
            string? executionId,
            string retrievalName,
            int rawItemCount,
            int finalItemCount,
            long durationMs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retrievalName);

            _realtime.LogInfo(
                message: $"RAG retrieval '{retrievalName}' merge completed.",
                category: "ai.rag.retrieval.merge.completed",
                data: new
                {
                    ExecutionId = executionId,
                    Retrieval = retrievalName,
                    RawItemCount = rawItemCount,
                    FinalItemCount = finalItemCount,
                    DurationMs = durationMs
                });
        }

        public void RetrievalCompleted(
            string? executionId,
            string retrievalName,
            RagRetrievalBatch batch,
            long durationMs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retrievalName);
            ArgumentNullException.ThrowIfNull(batch);

            _realtime.LogInfo(
                message: $"RAG retrieval '{retrievalName}' completed.",
                category: "ai.rag.retrieval.completed",
                data: new
                {
                    ExecutionId = executionId,
                    Retrieval = retrievalName,
                    ItemCount = batch.Items.Count,
                    DurationMs = durationMs,
                    batch.Diagnostics
                });
        }
    }
}