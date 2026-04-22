using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Data;
using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.Resolvers;

namespace Multiplexed.Sample.External.Plugins.Rag.Operations.Candidate
{
    /// <summary>
    /// Candidate retrieval operation supporting multiple providers.
    /// </summary>
    [RagOperation("candidate.byId", "relational")]
    public sealed class CandidateByIdOperation : IRagOperation<AiExecutionContext>
    {
        private readonly ICandidateRagDataSourceResolver _resolver;

        public CandidateByIdOperation(ICandidateRagDataSourceResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public string Key => "candidate.byId";

        public Type ExecutionContextType => typeof(AiExecutionContext);

        public async Task<RagRetrievalBatch> ExecuteUntypedAsync(
            object context,
            CancellationToken cancellationToken)
        {
            if (context is not IPluginExecutionContext<AiExecutionContext> typed)
            {
                throw new InvalidOperationException("Invalid execution context.");
            }

            return await ExecuteAsync(typed, cancellationToken);
        }

        public async Task<RagRetrievalBatch> ExecuteAsync(
            IPluginExecutionContext<AiExecutionContext> context,
            CancellationToken cancellationToken)
        {
            string candidateId = GetRequired(context.Inputs, "candidateId");

            string providerKey = context.Inputs.TryGetValue("providerKey", out var p)
                ? p?.ToString() ?? "sqlserver"
                : "sqlserver";

            string modeRaw = context.Inputs.TryGetValue("executionMode", out var m)
                ? m?.ToString() ?? "direct"
                : "direct";

            var executionMode = modeRaw == "provider"
                ? RagQueryExecutionMode.Provider
                : RagQueryExecutionMode.Direct;

            var dataSource = _resolver.Resolve(providerKey);

            var rows = await dataSource.QueryAsync(
                new RagQueryRequest
                {
                    Id = candidateId,
                    ExecutionMode = executionMode
                },
                cancellationToken);

            return BuildBatch(rows, candidateId, providerKey, "candidate");
        }

        private static RagRetrievalBatch BuildBatch(
            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
            string id,
            string providerKey,
            string retrievalKey)
        {
            var items = new List<RagNormalizedItem>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                items.Add(new RagNormalizedItem
                {
                    Id = $"{retrievalKey}:{id}:{i}",
                    ProviderKey = providerKey,
                    ProviderKind = RagProviderKind.Structured,
                    SourceType = RagProviderSourceType.Custom,
                    RetrievalKey = retrievalKey,
                    RetrievalKind = RagRetrievalKind.ById,
                    ContentType = "text/plain",
                    ContentText = string.Join("\n", row.Select(x => $"{x.Key}: {x.Value}")),
                    StableOrder = i
                });
            }

            return new RagRetrievalBatch
            {
                ProviderKey = providerKey,
                QueryKey = retrievalKey,
                Items = items
            };
        }

        private static string GetRequired(IReadOnlyDictionary<string, object?> inputs, string key)
        {
            if (!inputs.TryGetValue(key, out var value) || value is null)
            {
                throw new InvalidOperationException($"{key} is required.");
            }

            return value.ToString()!;
        }
    }
}