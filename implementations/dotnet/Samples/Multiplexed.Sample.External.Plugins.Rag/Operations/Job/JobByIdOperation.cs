using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Data;
using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Resolvers;

namespace Multiplexed.Sample.External.Plugins.Rag.Operations.Job
{
    /// <summary>
    /// Job retrieval operation supporting multiple providers.
    /// </summary>
    [RagOperation("job.byId", "relational")]
    public sealed class JobByIdOperation : IRagOperation<AiExecutionContext>
    {
        private readonly IJobRagDataSourceResolver _resolver;

        public JobByIdOperation(IJobRagDataSourceResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public string Key => "job.byId";

        public Type ExecutionContextType => typeof(AiExecutionContext);

        public async Task<RagRetrievalBatch> ExecuteUntypedAsync(object context, CancellationToken cancellationToken)
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
            string jobId = GetRequired(context.Inputs, "jobId");

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
                    Id = jobId,
                    ExecutionMode = executionMode
                },
                cancellationToken);

            return new RagRetrievalBatch
            {
                ProviderKey = providerKey,
                QueryKey = "job",
                Items = rows.Select((row, i) => new RagNormalizedItem
                {
                    Id = $"job:{jobId}:{i}",
                    ProviderKey = providerKey,
                    ProviderKind = RagProviderKind.Structured,
                    SourceType = RagProviderSourceType.Custom,
                    RetrievalKey = "job",
                    RetrievalKind = RagRetrievalKind.ById,
                    ContentType = "text/plain",
                    ContentText = string.Join("\n", row.Select(x => $"{x.Key}: {x.Value}")),
                    StableOrder = i
                }).ToList()
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