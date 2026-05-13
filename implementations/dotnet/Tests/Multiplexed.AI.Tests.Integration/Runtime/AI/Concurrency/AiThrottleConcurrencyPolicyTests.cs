using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.AI.Concurrency.Policies;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.AI.Concurrency
{
    /// <summary>
    /// Provides focused coverage for <see cref="AiThrottleConcurrencyPolicy"/>.
    /// </summary>
    /// <remarks>
    /// This policy is a marker/config policy. It does not enforce throttling directly.
    /// The resolver and throttle rule applicator convert its configuration into effective
    /// concurrency limits, and Redis enforces the distributed lease admission.
    /// </remarks>
    public sealed class AiThrottleConcurrencyPolicyTests
    {
        /// <summary>
        /// Verifies that the generic throttle policy allows execution and leaves distributed
        /// throttling enforcement to the Redis concurrency gate.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Allow_For_Generic_Throttle_Policy()
        {
            // Arrange
            var policy = new AiThrottleConcurrencyPolicy();

            var context = new AiConcurrencyPolicyContext
            {
                Concurrency = new AiConcurrencyContext
                {
                    ExecutionId = "exec-1",
                    PipelineKey = "pipeline-a:v1",
                    StepId = "step-a",
                    StepKey = "llm.summary",
                    RuntimeInstanceId = "worker-1",
                    LeaseId = "exec-1:step-a:worker-1",
                    Provider = "openai",
                    Model = "gpt-4.1",
                    Operation = "llm.chat"
                },
                Config = new Dictionary<string, object?>
                {
                    ["scope"] = "provider",
                    ["target"] = "openai",
                    ["limit"] = 10
                }
            };

            // Act
            var result = await policy.ExecuteAsync(context);

            // Assert
            var typedResult = Assert.IsType<AiPolicyResultGeneric<AiConcurrencyPolicyOutcome>>(result);

            Assert.NotNull(typedResult.Data);
            Assert.True(typedResult.Data!.IsAllowed);
            Assert.Null(typedResult.Data.Reason);
            Assert.Null(typedResult.Data.RetryAfter);
        }
    }
}