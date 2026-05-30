using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using Multiplexed.AI.Runtime.Execution.Persistence.Snapshot.Normalization;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Persistence
{
    /// <summary>
    /// Validates that Mongo-backed payloads remain recomposable after snapshot
    /// normalization and remapping.
    ///
    /// PURPOSE:
    /// - Prove externalized payloads are persisted in MongoDB
    /// - Prove snapshots only need to keep payload references
    /// - Prove remapped snapshots can still resolve original payload content
    ///
    /// IMPORTANT:
    /// - MongoDB is the durable source of truth for replay-safe payloads
    /// - Snapshot stores structure and payload references
    /// - Payload resolver recomposes the original content from MongoDB
    /// </summary>
    public sealed class PayloadSnapshotMongoIntegrationTests
    {
        [Fact]
        public async Task Snapshot_Should_Remain_Recomposable_With_Mongo_Payload_Store()
        {
            var connectionString = "mongodb://localhost:27017";

            var databaseName = "multiplexed_ai_tests";

            if (string.IsNullOrWhiteSpace(connectionString) ||
                string.IsNullOrWhiteSpace(databaseName))
            {
                return;
            }

            var services = new ServiceCollection();

            services.Configure<AiPayloadStoreOptions>(opts =>
            {
                opts.Enabled = true;
                opts.Provider = "mongo";
                opts.RequireReplaySafePayloads = true;

                opts.Mongo = new MongoAiPayloadStoreOptions
                {
                    Enabled = true,
                    ConnectionString = connectionString,
                    DatabaseName = databaseName,
                    CollectionName = "ai_execution_payloads_tests"
                };
            });

            services.TryAddSingleton<InMemoryAiPayloadStore>();
            services.TryAddSingleton<MongoAiPayloadStore>();
            services.TryAddSingleton<RedisCachedAiPayloadStore>();

            services.TryAddSingleton<IAiPayloadStoreResolver, DefaultAiPayloadStoreResolver>();
            services.TryAddSingleton<IAiExecutionDataPolicy, SmartInlineAiExecutionDataPolicy>();
            services.TryAddSingleton<IAiExecutionPayloadResolver, DefaultAiExecutionPayloadResolver>();

            var provider = services.BuildServiceProvider();

            var dataPolicy = provider.GetRequiredService<IAiExecutionDataPolicy>();
            var payloadResolver = provider.GetRequiredService<IAiExecutionPayloadResolver>();
            var storeResolver = provider.GetRequiredService<IAiPayloadStoreResolver>();

            var largeValue = new string('M', 5000);

            var payload = await dataPolicy.StoreAsync(largeValue);

            Assert.False(payload.IsInline);
            Assert.NotNull(payload.ArtifactId);

            var result = AiStepResult.Ok(
                value: new Dictionary<string, object?>
                {
                    ["payloadExternalized"] = true,
                    ["artifactId"] = payload.ArtifactId,
                    ["contentHash"] = payload.ContentHash,
                    ["sizeBytes"] = payload.SizeBytes,
                    ["contentType"] = payload.ContentType
                });

            result.Payload = payload;

            var state = new AiExecutionState
            {
                ExecutionId = "exec-payload-snapshot-test"
            };

            state.Steps["step-1"] = new AiStepState
            {
                StepName = "step-1",
                Result = result
            };

            var snapshot = new AiExecutionSnapshotDocument<object?>
            {
                ExecutionId = state.ExecutionId,
                Record = new AiExecutionRecord
                {
                    ExecutionId = state.ExecutionId,
                    PipelineName = "payload-snapshot-test"
                },
                State = state,
                ContextSnapshot = null
            };

            AiExecutionSnapshotNormalizer.Normalize(snapshot);
            AiExecutionSnapshotRemapper.Remap(snapshot);

            var remappedPayload =
                snapshot.State!.Steps["step-1"].Result!.Payload;

            Assert.NotNull(remappedPayload);
            Assert.False(remappedPayload!.IsInline);
            Assert.Equal(payload.ArtifactId, remappedPayload.ArtifactId);

            var resolved = await payloadResolver.ResolveAsync(remappedPayload);

            var json = Assert.IsType<JsonElement>(resolved);
            Assert.Equal(largeValue, json.GetString());

            await storeResolver.Resolve().DeleteAsync(payload.ArtifactId!);
        }
    }
}