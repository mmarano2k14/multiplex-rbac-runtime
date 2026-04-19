using System.Collections.Generic;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Represents the non-generic base execution context used by RAG retrieval
    /// and composition flows.
    ///
    /// PURPOSE:
    /// - Provides a stable orchestration envelope for RAG operations.
    /// - Carries generic execution metadata such as query text, tenant, user,
    ///   correlation identifiers, and dynamic inputs.
    /// - Keeps the core RAG contracts domain-agnostic.
    ///
    /// DESIGN:
    /// - This base type is intentionally non-generic so orchestration contracts
    ///   remain simple and stable.
    /// - A typed runtime snapshot can be attached through the generic derived
    ///   type <see cref="RagExecutionContext{TContextSnapshot}"/>.
    ///
    /// USAGE:
    /// - Used by retrieval orchestrators and providers that only require generic
    ///   execution information.
    /// - Can be safely passed across orchestration layers without forcing every
    ///   consumer to depend on a specific snapshot type.
    /// </summary>
    public class RagExecutionContext
    {
        /// <summary>
        /// Gets the raw query text associated with the current RAG execution.
        ///
        /// Examples:
        /// - a user search query
        /// - a system-generated retrieval prompt
        /// - a synthetic matching request
        /// </summary>
        public string QueryText { get; init; } = string.Empty;

        /// <summary>
        /// Gets an optional logical key describing the query category.
        ///
        /// This field can be used to distinguish different retrieval intents,
        /// such as matching, decision support, diagnostics, or summarization,
        /// without making the core RAG abstractions domain-specific.
        /// </summary>
        public string? QueryKey { get; init; }

        /// <summary>
        /// Gets the tenant identifier associated with the current execution.
        ///
        /// This supports multi-tenant retrieval policies and source filtering.
        /// </summary>
        public string? TenantId { get; init; }

        /// <summary>
        /// Gets the user identifier associated with the current execution.
        ///
        /// This can be used for user-scoped filtering, auditing, or provider-
        /// specific personalization where allowed by policy.
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// Gets the correlation identifier used for tracing and diagnostics.
        ///
        /// This value is useful for connecting retrieval activity to a broader
        /// workflow or execution trace.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Gets the optional maximum number of items that retrieval components
        /// should return or consider.
        ///
        /// This is a soft orchestration hint and may be interpreted differently
        /// by different providers or retrieval strategies.
        /// </summary>
        public int? MaxItems { get; init; }

        /// <summary>
        /// Gets the dynamic input values associated with the current execution.
        ///
        /// These inputs are intentionally flexible and may contain:
        /// - previous step outputs
        /// - pipeline state values
        /// - request-level parameters
        /// - externally supplied retrieval hints
        ///
        /// IMPORTANT:
        /// This bag is distinct from the typed snapshot carried by
        /// <see cref="RagExecutionContext{TContextSnapshot}"/>.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Inputs { get; init; }
            = new Dictionary<string, object?>();

        /// <summary>
        /// Gets additional execution metadata associated with the current RAG flow.
        ///
        /// This can be used for orchestration hints, diagnostics, provider-level
        /// options, or non-domain-specific tagging.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Metadata { get; init; }
            = new Dictionary<string, object?>();
    }
}