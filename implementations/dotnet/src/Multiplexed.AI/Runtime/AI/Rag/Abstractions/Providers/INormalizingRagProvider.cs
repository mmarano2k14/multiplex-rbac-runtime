using System.Threading;
using System.Threading.Tasks;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers
{
    /// <summary>
    /// Provider abstraction used by orchestration.
    /// 
    /// IMPORTANT:
    /// - Providers can be strongly typed internally
    /// - BUT must expose normalized output here
    /// </summary>
    public interface INormalizingRagProvider
    {
        /// <summary>
        /// Unique provider key (matches attribute).
        /// </summary>
        string Key { get; }

        Task<RagRetrievalBatch> RetrieveNormalizedAsync(
            RagExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}