using System;

namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Provides helper methods for working with typed
    /// <see cref="RagExecutionContext"/> instances.
    ///
    /// PURPOSE:
    /// - Simplifies access to the typed snapshot carried by
    ///   <see cref="RagExecutionContext{TContextSnapshot}"/>.
    /// - Avoids repeated casting logic inside providers and retrieval services.
    ///
    /// DESIGN:
    /// - These helpers keep orchestration contracts non-generic while still
    ///   allowing consumers to opt into strongly typed state access where needed.
    /// </summary>
    public static class RagExecutionContextExtensions
    {
        /// <summary>
        /// Attempts to retrieve the strongly typed snapshot from the specified
        /// execution context.
        ///
        /// If the supplied context is not an instance of
        /// <see cref="RagExecutionContext{TContextSnapshot}"/>, this method returns
        /// <see langword="default"/>.
        /// </summary>
        /// <typeparam name="TContextSnapshot">
        /// The expected snapshot type.
        /// </typeparam>
        /// <param name="context">
        /// The execution context to inspect.
        /// </param>
        /// <returns>
        /// The typed snapshot when present and compatible; otherwise
        /// <see langword="default"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        public static TContextSnapshot? GetContextSnapshot<TContextSnapshot>(
            this RagExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context is RagExecutionContext<TContextSnapshot> typedContext)
            {
                return typedContext.ContextSnapshot;
            }

            return default;
        }

        /// <summary>
        /// Retrieves the strongly typed snapshot from the specified execution context
        /// and throws if the context does not carry a compatible snapshot type.
        /// </summary>
        /// <typeparam name="TContextSnapshot">
        /// The required snapshot type.
        /// </typeparam>
        /// <param name="context">
        /// The execution context to inspect.
        /// </param>
        /// <returns>
        /// The typed snapshot associated with the current execution context.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the execution context does not contain a compatible typed
        /// snapshot.
        /// </exception>
        public static TContextSnapshot RequireContextSnapshot<TContextSnapshot>(
            this RagExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context is not RagExecutionContext<TContextSnapshot> typedContext)
            {
                throw new InvalidOperationException(
                    $"The RAG execution context does not contain a snapshot of type '{typeof(TContextSnapshot).FullName}'.");
            }

            return typedContext.ContextSnapshot!;
        }

        /// <summary>
        /// Determines whether the specified execution context carries a compatible
        /// strongly typed snapshot.
        /// </summary>
        /// <typeparam name="TContextSnapshot">
        /// The snapshot type to test.
        /// </typeparam>
        /// <param name="context">
        /// The execution context to inspect.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the context is an instance of
        /// <see cref="RagExecutionContext{TContextSnapshot}"/>; otherwise
        /// <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        public static bool HasContextSnapshot<TContextSnapshot>(
            this RagExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context is RagExecutionContext<TContextSnapshot>;
        }
    }
}