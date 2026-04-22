using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;
using Multiplexed.AI.Runtime.Rag.Operations;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    [RagOperation("test.operation", "sql")]
    public sealed class TestOperation : RagOperationBase<AiExecutionContext>
    {
        public override string Key => "test.operation";

        public override Task<RagRetrievalBatch> ExecuteAsync(
            IPluginExecutionContext<AiExecutionContext> context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            var query = context.Inputs.TryGetValue("query", out var queryValue)
                ? queryValue?.ToString()
                : null;

            var executionId = context.ExecutionContext.Record.ExecutionId;
            var pipelineName = context.ExecutionContext.Record.PipelineName;
            var snapshot = context.ExecutionContextSnapshot;

            return Task.FromResult(new RagRetrievalBatch
            {
                Items = new[]
                {
                    new RagNormalizedItem
                    {
                        Id = "test-1",
                        ProviderKey = "test.operation",
                        ContentType = "text/plain",
                        ContentText =
                            $"Test operation executed. Query='{query ?? string.Empty}', ExecutionId='{executionId}', Pipeline='{pipelineName}', HasSnapshot='{(snapshot is not null)}'.",
                        StableOrder = 0
                    }
                }
            });
        }
    }
}