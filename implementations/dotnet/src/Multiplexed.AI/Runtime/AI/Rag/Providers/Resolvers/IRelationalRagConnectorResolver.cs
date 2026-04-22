using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers.Resolvers
{
    /// <summary>
    /// Resolves a relational RAG connector by key.
    ///
    /// PURPOSE:
    /// - Decouples readers from direct connector construction.
    /// - Supports multiple relational backends inside the same runtime.
    ///
    /// DESIGN:
    /// - Resolution is key-based and deterministic.
    /// - Implementations typically rely on dependency injection.
    /// </summary>
    public interface IRelationalRagConnectorResolver
    {
        /// <summary>
        /// Resolves a relational connector by its unique key.
        /// </summary>
        /// <param name="connectorKey">
        /// The configured connector key.
        /// </param>
        /// <returns>
        /// The resolved <see cref="IRelationalRagConnector"/>.
        /// </returns>
        IRelationalRagConnector Resolve(string connectorKey);
    }
}