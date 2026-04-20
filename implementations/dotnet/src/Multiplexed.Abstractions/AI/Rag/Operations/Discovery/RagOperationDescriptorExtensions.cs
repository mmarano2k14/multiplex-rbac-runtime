namespace Multiplexed.Abstractions.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Helper methods for extracting RAG operation metadata from CLR types.
    /// </summary>
    public static class RagOperationDescriptorExtensions
    {
        /// <summary>
        /// Resolves the strongly typed execution context from an operation implementation type.
        /// </summary>
        /// <param name="implementationType">
        /// Concrete operation implementation type.
        /// </param>
        /// <returns>
        /// The strongly typed execution context expected by the operation.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the implementation type does not expose exactly one
        /// <see cref="IRagOperation{TContextSnapshot}"/> contract.
        /// </exception>
        public static Type ResolveExecutionContextType(Type implementationType)
        {
            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            var matchingInterfaces = implementationType
                .GetInterfaces()
                .Where(x =>
                    x.IsGenericType &&
                    x.GetGenericTypeDefinition() == typeof(IRagOperation<>))
                .ToArray();

            if (matchingInterfaces.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Type '{implementationType.FullName}' does not implement '{typeof(IRagOperation<>).FullName}'.");
            }

            if (matchingInterfaces.Length > 1)
            {
                var interfaceNames = string.Join(", ", matchingInterfaces.Select(x => x.FullName));

                throw new InvalidOperationException(
                    $"Type '{implementationType.FullName}' implements multiple IRagOperation<T> contracts: {interfaceNames}. " +
                    "A RAG operation must expose exactly one strongly typed execution context.");
            }

            return matchingInterfaces[0].GetGenericArguments()[0];
        }

        /// <summary>
        /// Validates that the implementation type is a concrete RAG operation type.
        /// </summary>
        /// <param name="implementationType">
        /// Type to validate.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the type is not a valid concrete operation implementation.
        /// </exception>
        public static void ValidateOperationImplementationType(Type implementationType)
        {
            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            if (!implementationType.IsClass)
            {
                throw new InvalidOperationException(
                    $"Type '{implementationType.FullName}' must be a class to be used as a RAG operation.");
            }

            if (implementationType.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Type '{implementationType.FullName}' is abstract and cannot be used as a RAG operation.");
            }

            if (!typeof(IRagOperation).IsAssignableFrom(implementationType))
            {
                throw new InvalidOperationException(
                    $"Type '{implementationType.FullName}' is marked as a RAG operation but does not implement '{typeof(IRagOperation).FullName}'.");
            }

            _ = ResolveExecutionContextType(implementationType);
        }
    }
}