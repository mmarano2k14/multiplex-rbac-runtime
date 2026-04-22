namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Marks a class as a dynamically discoverable RAG operation.
    ///
    /// PURPOSE:
    /// - Declares stable runtime metadata for a RAG operation.
    /// - Allows external/domain assemblies to register operations by key.
    /// - Associates an operation with the runtime provider that must execute
    ///   the underlying retrieval.
    ///
    /// DESIGN:
    /// - This attribute is intended for external/domain assemblies.
    /// - The runtime reads this metadata during discovery and transforms it
    ///   into deterministic runtime descriptors.
    /// - Provider selection is declared here so that business projects control
    ///   provider choice while the runtime remains infrastructure-only.
    ///
    /// IMPORTANT:
    /// - The provider key must match a registered runtime RAG provider.
    /// - The runtime must not infer or override this value dynamically.
    /// - Execution mode is explicit and defaults to Operation mode for backward compatibility.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RagOperationAttribute : Attribute
    {
        public RagOperationAttribute(string key, string providerKey, bool useProviderExecution = false)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Operation key cannot be null or whitespace.", nameof(key));

            if (string.IsNullOrWhiteSpace(providerKey))
                throw new ArgumentException("Provider key cannot be null or whitespace.", nameof(providerKey));

            Key = key;
            ProviderKey = providerKey;
            UseProviderExecution = useProviderExecution;
        }

        public string Key { get; }

        public string ProviderKey { get; }

        /// <summary>
        /// Gets a value indicating whether the runtime should execute this operation
        /// through the provider instead of invoking the operation directly.
        ///
        /// DEFAULT:
        /// - false → Operation mode (legacy behavior)
        /// - true  → Provider mode (runtime executes provider)
        /// </summary>
        public bool UseProviderExecution { get; }
    }
}