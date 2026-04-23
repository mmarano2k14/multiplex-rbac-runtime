using Multiplexed.AI.Runtime.Execution.Payloads;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Execution.Payloads
{
    public sealed class InlineAiExecutionDataPolicyTests
    {
        [Fact]
        public async Task StoreAsync_Should_Store_Value_Inline()
        {
            var policy = new InlineAiExecutionDataPolicy();

            var payload = await policy.StoreAsync(new { Score = 0.95 });

            Assert.True(payload.IsInline);
            Assert.NotNull(payload.InlineValue);
            Assert.Null(payload.ArtifactId);
        }
    }
}