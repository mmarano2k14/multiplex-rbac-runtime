using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Rag.Data
{
    /// <summary>
    /// Represents a generic RAG query request.
    ///
    /// PURPOSE:
    /// - Supports multiple query styles such as by-id, filter, and future search modes.
    /// - Allows the caller to choose an execution mode explicitly.
    /// </summary>
    public sealed class RagQueryRequest
    {
        public string? Id { get; init; }

        public string? QueryText { get; init; }

        public IReadOnlyDictionary<string, object?> Filters { get; init; }
            = new Dictionary<string, object?>();

        public int? Limit { get; init; }

        public RagQueryExecutionMode ExecutionMode { get; init; }
            = RagQueryExecutionMode.Direct;
    }
}