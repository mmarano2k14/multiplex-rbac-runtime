using Multiplexed.Abstractions.AI.Execution.Context;

namespace Multiplexed.Abstractions.AI.Rag.Models
{
    /// <summary>
    /// Strongly typed argument model for RAG retrieval steps.
    ///
    /// PURPOSE:
    /// - Provides a typed contract for common RAG retrieval arguments.
    /// - Keeps provider, execution mode, query, and orchestration metadata explicit.
    /// - Preserves domain-specific values through AdditionalInputs.
    ///
    /// DESIGN:
    /// - Known runtime/config values are exposed as typed properties.
    /// - Unknown values such as candidateId, jobId, tenantId, or custom operation inputs
    ///   are preserved in AdditionalInputs.
    /// </summary>
    public sealed class RagRetrievalArguments : IAiAdditionalInputsContainer
    {
        /// <summary>
        /// Gets or sets the provider key configured under provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the provider key configured under providerKey.
        /// </summary>
        public string? ProviderKey { get; set; }

        /// <summary>
        /// Gets or sets the retrieval execution mode.
        /// </summary>
        public string? ExecutionMode { get; set; }

        /// <summary>
        /// Gets or sets the query text or query path.
        /// </summary>
        public string? Query { get; set; }

        /// <summary>
        /// Gets or sets the upstream source step, when applicable.
        /// </summary>
        public string? SourceStep { get; set; }

        /// <summary>
        /// Gets or sets the composer key, when applicable.
        /// </summary>
        public string? Composer { get; set; }

        /// <summary>
        /// Gets the additional unmapped input values.
        ///
        /// EXAMPLES:
        /// - candidateId
        /// - jobId
        /// - tenantId
        /// - custom operation-specific arguments
        /// </summary>
        public Dictionary<string, object?> AdditionalInputs { get; } = new(StringComparer.Ordinal);
    }
}