
namespace Multiplexed.Abstractions.AI.Rag.Runtime
{
    /// <summary>
    /// Configuration for the generic RAG retrieval step.
    /// </summary>
    public sealed class RagRetrievalStepConfig
    {
        /// <summary>
        /// Gets or sets the unique RAG operation key to execute.
        ///
        /// Example:
        /// - getCandidate
        /// - getJob
        /// - getCandidateJobContext
        /// </summary>
        public string Operation { get; set; } = string.Empty;
    }
}