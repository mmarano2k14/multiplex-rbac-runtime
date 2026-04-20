// File: IRagOperation.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Rag.Models;

namespace Multiplexed.Abstractions.AI.Rag.Operations
{
    /// <summary>
    /// Non-typed runtime contract for dynamic RAG operations.
    /// </summary>
    public interface IRagOperation
    {
        /// <summary>
        /// Unique operation key (used in JSON config).
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Expected execution context type.
        /// </summary>
        Type ExecutionContextType { get; }

        /// <summary>
        /// Untyped execution entry point used by the runtime.
        /// </summary>
        Task<RagRetrievalBatch> ExecuteUntypedAsync(
            object context,
            CancellationToken cancellationToken);
    }
}