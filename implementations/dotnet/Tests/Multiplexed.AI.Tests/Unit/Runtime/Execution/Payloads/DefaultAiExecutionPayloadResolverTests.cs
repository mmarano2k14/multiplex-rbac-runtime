using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Execution.Payloads
{
    public sealed class DefaultAiExecutionPayloadResolverTests
    {
        [Fact]
        public async Task ResolveAsync_Should_Return_Inline_Value()
        {
            var resolver = new DefaultAiExecutionPayloadResolver();
            var payload = AiStoredPayload.Inline("hello");

            var value = await resolver.ResolveAsync(payload);

            Assert.Equal("hello", value);
        }

        [Fact]
        public async Task ResolveAsync_Should_Throw_For_Artifact_Payload()
        {
            var resolver = new DefaultAiExecutionPayloadResolver();
            var payload = AiStoredPayload.Artifact("artifact-1");

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                resolver.ResolveAsync(payload));
        }
    }
}