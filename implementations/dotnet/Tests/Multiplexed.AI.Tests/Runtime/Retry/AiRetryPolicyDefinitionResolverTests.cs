using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry;

namespace Multiplexed.AI.Tests.Runtime.AI.Retry
{
    public sealed class AiRetryPolicyDefinitionResolverTests
    {
        [Fact]
        public void Resolve_Should_Return_Null_When_Retry_Config_Is_Missing()
        {
            var resolver = new DefaultAiRetryPolicyDefinitionResolver();

            var result = resolver.Resolve(new Dictionary<string, object?>());

            Assert.Null(result);
        }

        [Fact]
        public void Resolve_Should_Parse_Retry_Config_With_Multiple_Policies()
        {
            var resolver = new DefaultAiRetryPolicyDefinitionResolver();

            var result = resolver.Resolve(
                new Dictionary<string, object?>
                {
                    ["retry"] = new Dictionary<string, object?>
                    {
                        ["policies"] = new[]
                        {
                            "retry.transient.redis",
                            "retry.transient.llm"
                        },
                        ["maxRetries"] = 5,
                        ["strategy"] = "exponential",
                        ["baseDelayMs"] = 200,
                        ["maxDelayMs"] = 5000,
                        ["jitter"] = true
                    }
                });

            Assert.NotNull(result);
            Assert.Equal(
                new[] { "retry.transient.redis", "retry.transient.llm" },
                result.Policies);
            Assert.Equal(5, result.MaxRetries);
            Assert.Equal(AiRetryBackoffStrategy.Exponential, result.Strategy);
            Assert.Equal(200, result.BaseDelayMs);
            Assert.Equal(5000, result.MaxDelayMs);
            Assert.True(result.Jitter);
        }

        [Fact]
        public void Resolve_Should_Parse_Single_Policy()
        {
            var resolver = new DefaultAiRetryPolicyDefinitionResolver();

            var result = resolver.Resolve(
                new Dictionary<string, object?>
                {
                    ["retry"] = new Dictionary<string, object?>
                    {
                        ["policy"] = "retry.transient.default"
                    }
                });

            Assert.NotNull(result);
            Assert.Equal(new[] { "retry.transient.default" }, result.Policies);
            Assert.Equal(3, result.MaxRetries);
            Assert.Equal(AiRetryBackoffStrategy.Fixed, result.Strategy);
            Assert.Equal(500, result.BaseDelayMs);
            Assert.Null(result.MaxDelayMs);
            Assert.False(result.Jitter);
        }
    }
}