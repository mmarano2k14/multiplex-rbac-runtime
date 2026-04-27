// File: RagRetrievalStepIntegrationTests.cs

using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Plugins;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Operations;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System;
using System.Collections.Generic;
using System.Text.Json;
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

            await engine.ExecuteAllAsync(created.ExecutionId);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            var payloadResolver =
                host.ServiceProvider.GetRequiredService<IAiExecutionPayloadResolver>();

            var step = state!.Steps["retrieve"];

            Assert.True(step.IsCompleted);
            Assert.NotNull(step.Result);

            var batch = await GetDataAsync<RagRetrievalBatch>(
                step.Result!,
                "batch",
                payloadResolver);

            Assert.NotNull(batch);
            Assert.NotNull(batch!.Items);
        }

        private static async Task<T?> GetDataAsync<T>(
            AiStepResult result,
            string key,
            IAiExecutionPayloadResolver resolver)
        {
            object? raw = null;

            if (result.DataPayloads is not null &&
                result.DataPayloads.TryGetValue(key, out var payload))
            {
                raw = await resolver.ResolveAsync(payload);
            }
            else if (result.Data is not null &&
                     result.Data.TryGetValue(key, out var value))
            {
                raw = value;
            }

            if (raw is null)
                return default;

            if (raw is T typed)
                return typed;

            if (raw is JsonElement json)
                return json.Deserialize<T>();

            return JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(raw));
        }

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
                    services.AddRagFromAssemblies(typeof(TestOperation).Assembly);
                });
        }

        private sealed class TestOperation : IRagOperation<AiExecutionContext>
        {
            public string Key => "test.operation";

            public Type ExecutionContextType => typeof(AiExecutionContext);

            public Task<RagRetrievalBatch> ExecuteAsync(
                IPluginExecutionContext<AiExecutionContext> context,
                CancellationToken cancellationToken)
            {
                Assert.NotNull(context.ExecutionContext);
                Assert.NotNull(context.Inputs);

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