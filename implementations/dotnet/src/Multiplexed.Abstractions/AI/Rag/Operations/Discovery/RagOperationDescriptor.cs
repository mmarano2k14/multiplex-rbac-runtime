namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Runtime descriptor for a discovered RAG operation.
    ///
    /// PURPOSE:
    /// - Transforms reflection-based discovery metadata into deterministic runtime metadata.
    /// - Provides a stable and immutable representation of a RAG operation.
    /// - Bridges external operation configuration with runtime execution infrastructure.
    ///
    /// DESIGN:
    /// - This descriptor is immutable after initialization.
    /// - All fields are required to guarantee deterministic resolution.
    /// - The descriptor is the single source of truth for operation metadata at runtime.
    ///
    /// IMPORTANT:
    /// - The <see cref="ProviderKey"/> defines which RAG provider must be used
    ///   for this operation.
    /// - This allows external projects to control provider selection without
    ///   coupling business logic to infrastructure.
    /// - The runtime MUST NOT infer or override this value dynamically.
    /// </summary>
    public sealed class RagOperationDescriptor
    {
        /// <summary>
        /// Gets the unique operation key.
        ///
        /// This key is used in pipeline configuration (e.g. JSON)
        /// and must be globally unique across all registered operations.
        /// </summary>
        public required string Key { get; init; }

        /// <summary>
        /// Gets the concrete implementation type of the operation.
        ///
        /// This type is resolved via dependency injection at runtime.
        /// </summary>
        public required Type ImplementationType { get; init; }

        /// <summary>
        /// Gets the strongly typed execution context expected by the operation.
        ///
        /// The runtime uses this to enforce type safety when invoking
        /// the operation through the dispatcher.
        /// </summary>
        public required Type ExecutionContextType { get; init; }

        /// <summary>
        /// Gets the provider key associated with this operation.
        ///
        /// PURPOSE:
        /// - Defines which <see cref="INormalizingRagProvider"/> must be used
        ///   to perform the underlying retrieval.
        ///
        /// DESIGN:
        /// - This value is resolved via the provider registry and dependency injection.
        /// - It must match a registered provider key.
        ///
        /// IMPORTANT:
        /// - This value is defined by the external project (business configuration),
        ///   not by the runtime.
        /// - This ensures clear separation between:
        ///     - operation intent (external)
        ///     - provider implementation (runtime)
        /// </summary>
        public required string ProviderKey { get; init; }
        /// <summary>
        /// Gets a value indicating whether the runtime should execute
        /// this operation using the provider path.
        /// </summary>
        public required bool UseProviderExecution { get; init; }
    }
}