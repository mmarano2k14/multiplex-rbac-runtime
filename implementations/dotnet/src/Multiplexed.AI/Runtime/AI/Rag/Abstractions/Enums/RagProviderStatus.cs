namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums
{
    /// <summary>
    /// Represents the runtime availability or maturity state of a RAG provider.
    ///
    /// PURPOSE:
    /// - Exposes provider lifecycle and usage metadata to discovery,
    ///   registration, diagnostics, and tooling layers.
    /// - Allows the system to distinguish between fully supported providers
    ///   and providers that are disabled, experimental, or deprecated.
    ///
    /// DESIGN:
    /// - This enum describes provider metadata, not health-check results.
    /// - It is intended for configuration, discovery, and registry behavior,
    ///   not for transient runtime failure tracking.
    ///
    /// USAGE:
    /// - Used by the RAG provider attribute to describe the declared status
    ///   of a provider implementation.
    /// - Can later be used by registries or DI registration policies to filter
    ///   which providers are exposed or selected.
    /// </summary>
    public enum RagProviderStatus
    {
        /// <summary>
        /// The provider status is not specified.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The provider is enabled and intended for normal use.
        /// </summary>
        Enabled = 1,

        /// <summary>
        /// The provider is disabled and should not be selected by default.
        /// </summary>
        Disabled = 2,

        /// <summary>
        /// The provider is available for testing or limited usage,
        /// but should be considered unstable or not yet finalized.
        /// </summary>
        Experimental = 3,

        /// <summary>
        /// The provider is still present for compatibility reasons,
        /// but should no longer be used for new configurations.
        /// </summary>
        Deprecated = 4
    }
}