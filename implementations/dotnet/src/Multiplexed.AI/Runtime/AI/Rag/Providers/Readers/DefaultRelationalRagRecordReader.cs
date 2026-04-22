using Multiplexed.AI.Runtime.AI.Rag.Providers.Resolvers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers.Readers
{
    /// <summary>
    /// Default implementation of <see cref="IRelationalRagRecordReader"/>.
    ///
    /// PURPOSE:
    /// - Provides a standard runtime implementation of relational reading.
    /// - Delegates actual data access to a resolved relational connector.
    ///
    /// DESIGN:
    /// - This class is backend-agnostic.
    /// - Connector selection is key-based and deterministic.
    /// - Keeps providers independent from connector implementations.
    ///
    /// RESPONSIBILITY:
    /// - Validate input parameters.
    /// - Resolve the appropriate connector.
    /// - Forward the read operation to that connector.
    ///
    /// IMPORTANT:
    /// - Does not perform RAG normalization.
    /// - Does not contain domain-specific logic.
    /// - Must preserve deterministic execution.
    /// </summary>
    public sealed class DefaultRelationalRagRecordReader : IRelationalRagRecordReader
    {
        private readonly IRelationalRagConnectorResolver _connectorResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRelationalRagRecordReader"/> class.
        /// </summary>
        /// <param name="connectorResolver">
        /// The relational connector resolver used to select the backend.
        /// </param>
        public DefaultRelationalRagRecordReader(
            IRelationalRagConnectorResolver connectorResolver)
        {
            _connectorResolver = connectorResolver ?? throw new ArgumentNullException(nameof(connectorResolver));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadAsync(
            string connectorKey,
            string entityType,
            string entityId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectorKey))
            {
                throw new ArgumentException("Connector key cannot be null or whitespace.", nameof(connectorKey));
            }

            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ArgumentException("Entity type cannot be null or whitespace.", nameof(entityType));
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException("Entity id cannot be null or whitespace.", nameof(entityId));
            }

            var connector = _connectorResolver.Resolve(connectorKey);

            return connector.ReadAsync(entityType, entityId, cancellationToken);
        }
    }
}