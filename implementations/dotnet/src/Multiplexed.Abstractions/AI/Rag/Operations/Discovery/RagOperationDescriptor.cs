namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Runtime descriptor for a discovered RAG operation.
    ///
    /// This object exists to transform reflection metadata into
    /// deterministic runtime metadata.
    /// </summary>
    public sealed class RagOperationDescriptor
    {
        /// <summary>
        /// Gets or sets the unique operation key.
        /// </summary>
        public required string Key { get; init; }

        /// <summary>
        /// Gets or sets the concrete implementation type.
        /// </summary>
        public required Type ImplementationType { get; init; }

        /// <summary>
        /// Gets or sets the strongly typed execution context expected by the operation.
        /// </summary>
        public required Type ExecutionContextType { get; init; }
    }
}