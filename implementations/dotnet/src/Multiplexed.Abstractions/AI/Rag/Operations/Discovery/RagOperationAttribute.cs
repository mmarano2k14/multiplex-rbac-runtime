namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Marks a class as a dynamically discoverable RAG operation.
    ///
    /// This attribute is intended for external/domain assemblies.
    /// The runtime uses it to register operations by key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RagOperationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RagOperationAttribute"/> class.
        /// </summary>
        /// <param name="key">
        /// Unique operation key.
        /// </param>
        public RagOperationAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Operation key cannot be null or whitespace.", nameof(key));
            }

            Key = key;
        }

        /// <summary>
        /// Gets the unique operation key.
        /// </summary>
        public string Key { get; }
    }
}