using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Payloads;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Execution.Payloads
{
    /// <summary>
    /// Validates payload compaction behavior.
    ///
    /// PURPOSE:
    /// - Ensure large primary values are externalized
    /// - Ensure large structured data entries are externalized
    /// - Ensure inline values are replaced with compact summaries
    /// - Ensure artifact payloads remain resolvable
    ///
    /// IMPORTANT:
    /// - This test no longer uses AiStepExecutor.
    /// - Payload compaction is now centralized via IAiStepResultPayloadCompactor.
    /// - This ensures ALL execution paths (DAG / RAG / operations) behave consistently.
    /// </summary>
    public sealed class AiStepExecutorPayloadCompactionTests
    {
        [Fact]
        public async Task ExecuteAsync_Should_Externalize_Large_Payload_And_Replace_Value_With_Summary()
        {
            var store = new InMemoryAiPayloadStore();
            var storeResolver = new FixedAiPayloadStoreResolver(store);

            var options = Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = "inmemory",
                RequireReplaySafePayloads = false,
                MaxInlineSizeBytes = 2048
            });

            var dataPolicy = new SmartInlineAiExecutionDataPolicy(storeResolver, options);
            var compactor = new DefaultAiStepResultPayloadCompactor(dataPolicy);
            var resolver = new DefaultAiExecutionPayloadResolver(storeResolver);

            var largeValue = new string('A', 5000);
            var result = AiStepResult.Ok(value: largeValue);

            await compactor.CompactAsync(result);

            Assert.NotNull(result.Payload);
            Assert.False(result.Payload!.IsInline);

            var summary = Assert.IsType<Dictionary<string, object?>>(result.Value);
            Assert.True((bool)summary["payloadExternalized"]!);
            Assert.NotNull(summary["artifactId"]);

            var resolved = await resolver.ResolveAsync(result.Payload);

            var json = Assert.IsType<JsonElement>(resolved);
            Assert.Equal(largeValue, json.GetString());
        }

        [Fact]
        public async Task ExecuteAsync_Should_Externalize_Large_Data_Entries_And_Replace_Data_With_Summary()
        {
            var store = new InMemoryAiPayloadStore();
            var storeResolver = new FixedAiPayloadStoreResolver(store);

            var options = Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = "inmemory",
                RequireReplaySafePayloads = false,
                MaxInlineSizeBytes = 2048
            });

            var dataPolicy = new SmartInlineAiExecutionDataPolicy(storeResolver, options);
            var compactor = new DefaultAiStepResultPayloadCompactor(dataPolicy);
            var resolver = new DefaultAiExecutionPayloadResolver(storeResolver);

            var largeDataValue = new string('B', 5000);

            var result = AiStepResult.Ok(
                value: null,
                data: new Dictionary<string, object?>
                {
                    ["big"] = largeDataValue
                });

            await compactor.CompactAsync(result);

            Assert.NotNull(result.DataPayloads);
            Assert.True(result.DataPayloads!.ContainsKey("big"));

            var payload = result.DataPayloads["big"];
            Assert.False(payload.IsInline);

            var summary = Assert.IsType<Dictionary<string, object?>>(result.Data["big"]);
            Assert.True((bool)summary["payloadExternalized"]!);
            Assert.NotNull(summary["artifactId"]);

            var resolved = await resolver.ResolveAsync(payload);

            var json = Assert.IsType<JsonElement>(resolved);
            Assert.Equal(largeDataValue, json.GetString());
        }
    }
}