using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Tests.Runtime.Execution.Payloads;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Execution.Payloads
{
    /// <summary>
    /// Unit tests for <see cref="DefaultAiExecutionPayloadResolver"/>.
    ///
    /// PURPOSE:
    /// - Validate inline payload resolution
    /// - Validate artifact resolution behavior
    /// - Ensure backward compatibility with existing inline payload usage
    ///
    /// IMPORTANT:
    /// - Artifact payloads are now supported (breaking previous NotSupported behavior)
    /// - Missing artifacts are treated as invalid runtime state
    /// </summary>
    public sealed class DefaultAiExecutionPayloadResolverTests
    {
        /// <summary>
        /// Ensures that inline payloads are returned directly without modification.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_Should_Return_Inline_Value()
        {

            var store = new InMemoryAiPayloadStore();
            var storeResolver = new FixedAiPayloadStoreResolver(store);
            var resolver = new DefaultAiExecutionPayloadResolver(storeResolver);

            var payload = AiStoredPayload.Inline("hello");

            var value = await resolver.ResolveAsync(payload);

            Assert.Equal("hello", value);
        }

        /// <summary>
        /// Ensures that resolving a missing artifact throws a deterministic exception.
        ///
        /// BEHAVIOR:
        /// - Artifact-backed payloads are now supported
        /// - If the artifact does not exist in the store, this is treated as invalid state
        /// </summary>
        [Fact]
        public async Task ResolveAsync_Should_Throw_When_Artifact_Is_Missing()
        {
            var store = new InMemoryAiPayloadStore();
            var storeResolver = new FixedAiPayloadStoreResolver(store);
            var resolver = new DefaultAiExecutionPayloadResolver(storeResolver);
            var payload = AiStoredPayload.Artifact("artifact-1");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                resolver.ResolveAsync(payload));
        }

        /// <summary>
        /// Ensures that an existing artifact is correctly loaded and deserialized.
        ///
        /// BEHAVIOR:
        /// - Payload is stored as JSON string
        /// - Resolver returns a JsonElement (consistent with existing deserialization pipeline)
        /// </summary>
        [Fact]
        public async Task ResolveAsync_Should_Return_Artifact_Content_When_Artifact_Exists()
        {
            var store = new InMemoryAiPayloadStore();
            var storeResolver = new FixedAiPayloadStoreResolver(store);
            var resolver = new DefaultAiExecutionPayloadResolver(storeResolver);

            // Store JSON string manually (as done by future policy)
            var artifactId = await store.SaveAsync("\"hello\"");

            
            var payload = AiStoredPayload.Artifact(artifactId);

            var value = await resolver.ResolveAsync(payload);

            var json = Assert.IsType<JsonElement>(value);
            Assert.Equal("hello", json.GetString());
        }

        [Fact]
        public async Task StoreAsync_Should_Use_Artifact_When_Payload_Is_Large()
        {
            var store = new InMemoryAiPayloadStore();
            var storeResolver = new FixedAiPayloadStoreResolver(store);
            var options = Options.Create(new AiPayloadStoreOptions
            {
                MaxInlineSizeBytes = 2048 // Set max inline size to 2KB for testing
            });

            var policy = new SmartInlineAiExecutionDataPolicy(storeResolver, options);


            var large = new string('A', 5000);

            var payload = await policy.StoreAsync(large);

            Assert.False(payload.IsInline);
            Assert.NotNull(payload.ArtifactId);
        }
    }
}