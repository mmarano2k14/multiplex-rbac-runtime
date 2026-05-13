using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.AI.Concurrency.Policies;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.AI.Concurrency
{
    /// <summary>
    /// Provides focused coverage for <see cref="AiModelAdmissionConcurrencyPolicy"/>.
    /// </summary>
    public sealed class AiModelAdmissionConcurrencyPolicyTests
    {
        /// <summary>
        /// Verifies that the policy allows execution when the provider/model pair is allowed.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Allow_When_Model_Is_Allowed()
        {
            var policy = new AiModelAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "openai",
                model: "gpt-4.1",
                config: new Dictionary<string, object?>
                {
                    ["allowedModels"] = new[] { "openai:gpt-4.1", "openai:gpt-4o" }
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.True(outcome.IsAllowed);
            Assert.Null(outcome.Reason);
            Assert.Null(outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that the policy denies execution when the provider/model pair is blocked.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Deny_When_Model_Is_Blocked()
        {
            var policy = new AiModelAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "openai",
                model: "gpt-3.5",
                config: new Dictionary<string, object?>
                {
                    ["blockedModels"] = new[] { "openai:gpt-3.5" },
                    ["retryAfterMs"] = 500
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.False(outcome.IsAllowed);
            Assert.Contains("blocked", outcome.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TimeSpan.FromMilliseconds(500), outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that the policy denies execution when a model is required but missing.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Deny_When_Model_Is_Required_But_Missing()
        {
            var policy = new AiModelAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "openai",
                model: null,
                config: new Dictionary<string, object?>
                {
                    ["requireModel"] = true,
                    ["reason"] = "Model is required for this pipeline.",
                    ["retryAfterMs"] = 250
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.False(outcome.IsAllowed);
            Assert.Equal("Model is required for this pipeline.", outcome.Reason);
            Assert.Equal(TimeSpan.FromMilliseconds(250), outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that provider/model matching is case-insensitive.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Compare_Models_Case_Insensitively()
        {
            var policy = new AiModelAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "OpenAI",
                model: "GPT-4.1",
                config: new Dictionary<string, object?>
                {
                    ["allowedModels"] = new[] { "openai:gpt-4.1" }
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.True(outcome.IsAllowed);
        }

        /// <summary>
        /// Verifies that the same model name under a different provider is treated independently.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Scope_Model_By_Provider()
        {
            var policy = new AiModelAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "anthropic",
                model: "gpt-4.1",
                config: new Dictionary<string, object?>
                {
                    ["allowedModels"] = new[] { "openai:gpt-4.1" }
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.False(outcome.IsAllowed);
            Assert.Contains("not allowed", outcome.Reason, StringComparison.OrdinalIgnoreCase);
        }

        private static AiConcurrencyPolicyContext CreateContext(
            string? provider,
            string? model,
            IReadOnlyDictionary<string, object?> config)
        {
            return new AiConcurrencyPolicyContext
            {
                Concurrency = new AiConcurrencyContext
                {
                    ExecutionId = "exec-1",
                    PipelineKey = "pipeline-a:v1",
                    StepId = "step-a",
                    StepKey = "llm.summary",
                    RuntimeInstanceId = "worker-1",
                    LeaseId = "exec-1:step-a:worker-1",
                    Provider = provider,
                    Model = model,
                    Operation = "llm.chat"
                },
                Config = config
            };
        }

        private static AiConcurrencyPolicyOutcome GetOutcome(
            AiPolicyResult result)
        {
            var typedResult = Assert.IsType<AiPolicyResultGeneric<AiConcurrencyPolicyOutcome>>(result);

            Assert.NotNull(typedResult.Data);

            return typedResult.Data;
        }
    }
}