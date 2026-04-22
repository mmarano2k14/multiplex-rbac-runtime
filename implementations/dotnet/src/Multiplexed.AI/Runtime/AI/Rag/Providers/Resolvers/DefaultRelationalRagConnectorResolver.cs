using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers.Resolvers
{
    /// <summary>
    /// Default implementation of <see cref="IRelationalRagConnectorResolver"/>.
    ///
    /// PURPOSE:
    /// - Resolves relational connectors by their configured key.
    /// - Supports multiple relational backends within the same runtime instance.
    /// - Provides deterministic connector lookup for relational readers.
    ///
    /// DESIGN:
    /// - All available connectors are injected through dependency injection.
    /// - Connectors are indexed by key using ordinal string comparison.
    /// - Resolution fails fast on invalid configuration.
    ///
    /// IMPORTANT:
    /// - Connector keys must be unique.
    /// - Resolution must remain deterministic.
    /// - This resolver does not create connectors manually.
    /// </summary>
    public sealed class DefaultRelationalRagConnectorResolver : IRelationalRagConnectorResolver
    {
        private readonly IReadOnlyDictionary<string, IRelationalRagConnector> _connectors;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRelationalRagConnectorResolver"/> class.
        /// </summary>
        /// <param name="connectors">
        /// The registered relational connectors.
        /// </param>
        public DefaultRelationalRagConnectorResolver(
            IEnumerable<IRelationalRagConnector> connectors)
        {
            ArgumentNullException.ThrowIfNull(connectors);

            var connectorArray = connectors.ToArray();

            ValidateConnectors(connectorArray);

            _connectors = connectorArray
                .ToDictionary(
                    x => x.Key,
                    x => x,
                    StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public IRelationalRagConnector Resolve(string connectorKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectorKey);

            if (!_connectors.TryGetValue(connectorKey, out var connector))
            {
                throw new KeyNotFoundException(
                    $"No relational RAG connector is registered for key '{connectorKey}'.");
            }

            return connector;
        }

        /// <summary>
        /// Validates the specified connector collection before resolver initialization.
        /// </summary>
        /// <param name="connectors">
        /// The connectors to validate.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the connector collection contains invalid or duplicate entries.
        /// </exception>
        private static void ValidateConnectors(
            IEnumerable<IRelationalRagConnector> connectors)
        {
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var connector in connectors)
            {
                if (connector is null)
                {
                    throw new InvalidOperationException(
                        "Relational RAG connectors cannot contain null entries.");
                }

                if (string.IsNullOrWhiteSpace(connector.Key))
                {
                    throw new InvalidOperationException(
                        "Relational RAG connector key cannot be null, empty, or whitespace.");
                }

                if (!seenKeys.Add(connector.Key))
                {
                    throw new InvalidOperationException(
                        $"Duplicate relational RAG connector key '{connector.Key}' was detected.");
                }
            }
        }
    }
}