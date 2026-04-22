using System.Globalization;
using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Providers;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Readers;

namespace Multiplexed.AI.Runtime.AI.Rag.Providers
{
    /// <summary>
    /// Relational-backed RAG provider that retrieves structured rows and normalizes them
    /// into a deterministic <see cref="RagRetrievalBatch"/>.
    ///
    /// PURPOSE:
    /// - Acts as a runtime infrastructure provider for relational RAG retrieval.
    /// - Converts structured relational results into normalized orchestration-compatible items.
    /// - Supports provider-mode execution without coupling business projects to backend details.
    ///
    /// DESIGN:
    /// - Retrieval input is read from <see cref="RagExecutionContext"/>.
    /// - The provider depends on a higher-level relational record reader abstraction.
    /// - Connector selection is delegated to the reader layer through a connector key.
    /// - Output ordering is deterministic.
    ///
    /// IMPORTANT:
    /// - This provider does not implement business-specific query composition.
    /// - It expects the caller to provide a valid connector key, entity type, and entity identifier.
    /// - Returned items must remain stable for replay, logging, and downstream merge/rank flows.
    /// </summary>
    public sealed class RelationalRagProvider : INormalizingRagProvider
    {
        private readonly IRelationalRagRecordReader _recordReader;

        public RelationalRagProvider(IRelationalRagRecordReader recordReader)
        {
            _recordReader = recordReader ?? throw new ArgumentNullException(nameof(recordReader));
        }

        /// <inheritdoc />
        public string Key => "relational";

        /// <inheritdoc />
        public async Task<RagRetrievalBatch> RetrieveNormalizedAsync(
            RagExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            string connectorKey = GetRequiredString(context.Inputs, "connectorKey");
            string entityType = GetRequiredString(context.Inputs, "entityType");
            string entityId = GetRequiredString(context.Inputs, "entityId");

            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
                await _recordReader.ReadAsync(connectorKey, entityType, entityId, cancellationToken)
                    .ConfigureAwait(false);

            var items = new List<RagNormalizedItem>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                IReadOnlyDictionary<string, object?> row = rows[i];

                string sourceId = TryGetString(row, "Id")
                    ?? TryGetString(row, "id")
                    ?? entityId;

                string contentText = BuildDeterministicContentText(row);

                items.Add(new RagNormalizedItem
                {
                    Id = $"{entityType}:{sourceId}:{i.ToString(CultureInfo.InvariantCulture)}",
                    ProviderKey = Key,
                    ProviderKind = RagProviderKind.Structured,
                    SourceType = RagProviderSourceType.Custom,
                    RetrievalKey = entityType,
                    RetrievalKind = RagRetrievalKind.ById,
                    ContentType = "text/plain",
                    ContentText = contentText,
                    Score = null,
                    Payload = null,
                    StableOrder = i,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["connectorKey"] = connectorKey,
                        ["entityType"] = entityType,
                        ["entityId"] = entityId,
                        ["sourceId"] = sourceId,
                        ["queryKey"] = context.QueryKey
                    }
                });
            }

            return new RagRetrievalBatch
            {
                ProviderKey = Key,
                QueryKey = context.QueryKey,
                QueryText = context.QueryText,
                Items = items,
                Diagnostics = null,
                Metadata = new Dictionary<string, object?>
                {
                    ["connectorKey"] = connectorKey,
                    ["entityType"] = entityType,
                    ["entityId"] = entityId,
                    ["itemCount"] = items.Count
                }
            };
        }

        private static string GetRequiredString(
            IReadOnlyDictionary<string, object?> inputs,
            string key)
        {
            if (!inputs.TryGetValue(key, out object? value) ||
                value is null ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                throw new InvalidOperationException(
                    $"Required relational RAG input '{key}' is missing.");
            }

            return value.ToString()!;
        }

        private static string? TryGetString(
            IReadOnlyDictionary<string, object?> values,
            string key)
        {
            if (!values.TryGetValue(key, out object? value) || value is null)
            {
                return null;
            }

            return value.ToString();
        }

        /// <summary>
        /// Builds deterministic text content from a structured row.
        ///
        /// RULES:
        /// - stable key ordering
        /// - ignores null values
        /// - produces replay-safe text output
        /// </summary>
        private static string BuildDeterministicContentText(
            IReadOnlyDictionary<string, object?> row)
        {
            var pairs = row
                .Where(x => x.Value is not null)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}: {x.Value}");

            return string.Join(Environment.NewLine, pairs);
        }
    }
}