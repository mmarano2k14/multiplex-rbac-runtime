using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Execution.Payloads
{
    /// <summary>
    /// Validates the runtime payload read/write path through DI.
    ///
    /// PURPOSE:
    /// - Ensure IAiExecutionDataPolicy uses IAiPayloadStoreResolver.
    /// - Ensure IAiExecutionPayloadResolver can recompose externalized payloads.
    /// - Prove the payload store is not only used by unit tests but is connected through runtime DI.
    ///
    /// IMPORTANT:
    /// - This test uses InMemory provider only.
    /// - Mongo/Redis runtime integration should be tested separately.
    /// - The goal here is to validate the provider selection chain.
    /// </summary>
    public sealed class PayloadStoreRuntimeResolverTests
    {
        [Fact]
        public async Task Payload_Runtime_Path_Should_Externalize_And_Recompose_Large_Value()
        {
            // ---------------------------------------------------------
            // Arrange
            // ---------------------------------------------------------
            var services = new ServiceCollection();

            services.Configure<AiPayloadStoreOptions>(opts =>
            {
                opts.Enabled = true;
                opts.Provider = "inmemory";
                opts.RequireReplaySafePayloads = false;
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

            var largeValue = new string('X', 5000);

            // ---------------------------------------------------------
            // Act - write path
            // ---------------------------------------------------------
            var payload = await dataPolicy.StoreAsync(largeValue);

            // ---------------------------------------------------------
            // Assert - externalization happened
            // ---------------------------------------------------------
            Assert.NotNull(payload);
            Assert.False(payload.IsInline);
            Assert.False(string.IsNullOrWhiteSpace(payload.ArtifactId));

            // ---------------------------------------------------------
            // Act - read path / recomposition
            // ---------------------------------------------------------
            var resolved = await payloadResolver.ResolveAsync(payload);

            // ---------------------------------------------------------
            // Assert - payload was recomposed from the configured store
            // ---------------------------------------------------------
            var json = Assert.IsType<JsonElement>(resolved);
            Assert.Equal(largeValue, json.GetString());
        }
    }
}