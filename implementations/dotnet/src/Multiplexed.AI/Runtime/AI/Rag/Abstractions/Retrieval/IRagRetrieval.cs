using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Retrieval
{
    /// <summary>
    /// High-level retrieval abstraction.
    /// 
    /// This orchestrates one or more providers.
    /// </summary>
    public interface IRagRetrieval
    {
        Task<RagRetrievalBatch> RetrieveAsync(
            RagExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}