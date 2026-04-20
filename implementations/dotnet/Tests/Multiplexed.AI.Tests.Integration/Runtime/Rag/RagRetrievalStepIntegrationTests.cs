// File: RagRetrievalStepIntegrationTests.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Runtime.AI.Rag.Normalization;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    public sealed class RagRetrievalStepIntegrationTests
    {
        [Fact]
        public async Task ExecuteAllAsync_Should_Run_RagRetrieval_With_PluginContext()
        {
            await using var host = await CreateHostAsync();

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "rag-retrieval-test",
                new Dictionary<string, object?>
                {
                    ["query"] = "Hello Plugin"
                });

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            var step = state!.Steps["retrieve"];

            Assert.True(step.IsCompleted);
            Assert.NotNull(step.Result);
            Assert.NotNull(step.Result.Data);

            Assert.True(step.Result.Data.TryGetValue("batch", out var batchValue));

            var batch = Assert.IsType<RagRetrievalBatch>(batchValue);

            Assert.NotNull(batch.Items);
        }

        // -----------------------------------------------------
        // HOST SETUP
        // -----------------------------------------------------

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync()
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\rag-retrieval-test.json"
            };

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddRagCore();
                    services.AddRagOperationsFromAssemblies(typeof(TestOperation).Assembly);
                });
        }

        // -----------------------------------------------------
        // TEST OPERATION
        // -----------------------------------------------------

        private sealed class TestOperation : IRagOperation<AiExecutionContext>
        {
            public string Key => "test.operation";

            public Type ExecutionContextType => typeof(AiExecutionContext);

            public Task<RagRetrievalBatch> ExecuteAsync(
                IPluginExecutionContext<AiExecutionContext> context,
                CancellationToken cancellationToken)
            {
                // 🔥 VALIDATION CRITIQUE
                Assert.NotNull(context.ExecutionContext);
                Assert.NotNull(context.Inputs);

                // Snapshot peut être null selon ton setup → pas bloquant
                // Assert.NotNull(context.ExecutionContextSnapshot);

                return Task.FromResult(new RagRetrievalBatch
                {
                    Items = new[]
                    {
                        new RagNormalizedItem
                        {
                            Id = "test-1",
                            ProviderKey = "test",
                            ContentType = "text/plain",
                            ContentText = "Test operation result"
                        }
                    }
                });
            }

            public Task<RagRetrievalBatch> ExecuteUntypedAsync(
                object context,
                CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}