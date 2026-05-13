using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.AI.Concurrency.Policies;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.AI.Concurrency
{
    /// <summary>
    /// Provides focused coverage for <see cref="AiProviderAdmissionConcurrencyPolicy"/>.
    /// </summary>
    /// <remarks>
    /// These tests validate the policy in isolation, without Redis, DAG claiming, or the
    /// concurrency engine orchestration layer.
    /// </remarks>
    public sealed class AiProviderAdmissionConcurrencyPolicyTests
    {
        /// <summary>
        /// Verifies that the policy allows execution when the provider is included
        /// in the configured allow list.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Allow_When_Provider_Is_Allowed()
        {
            // Arrange
            var policy = new AiProviderAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "openai",
                config: new Dictionary<string, object?>
                {
                    ["allowedProviders"] = new[] { "openai", "anthropic" }
                });

            // Act
            var result = await policy.ExecuteAsync(context);

            // Assert
            var outcome = GetOutcome(result);

            Assert.True(outcome.IsAllowed);
            Assert.Null(outcome.Reason);
            Assert.Null(outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that the policy denies execution when the provider is included
        /// in the configured block list.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Deny_When_Provider_Is_Blocked()
        {
            // Arrange
            var policy = new AiProviderAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "legacy-provider",
                config: new Dictionary<string, object?>
                {
                    ["blockedProviders"] = new[] { "legacy-provider" },
                    ["retryAfterMs"] = 500
                });

            // Act
            var result = await policy.ExecuteAsync(context);

            // Assert
            var outcome = GetOutcome(result);

            Assert.False(outcome.IsAllowed);
            Assert.Contains("blocked", outcome.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TimeSpan.FromMilliseconds(500), outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that the policy denies execution when a provider is required
        /// but no provider is present in the concurrency context.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Deny_When_Provider_Is_Required_But_Missing()
        {
            // Arrange
            var policy = new AiProviderAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: null,
                config: new Dictionary<string, object?>
                {
                    ["requireProvider"] = true,
                    ["reason"] = "Provider is required for this pipeline.",
                    ["retryAfterMs"] = 250
                });

            // Act
            var result = await policy.ExecuteAsync(context);

            // Assert
            var outcome = GetOutcome(result);

            Assert.False(outcome.IsAllowed);
            Assert.Equal("Provider is required for this pipeline.", outcome.Reason);
            Assert.Equal(TimeSpan.FromMilliseconds(250), outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that provider comparison is case-insensitive.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Compare_Providers_Case_Insensitively()
        {
            // Arrange
            var policy = new AiProviderAdmissionConcurrencyPolicy();

            var context = CreateContext(
                provider: "OpenAI",
                config: new Dictionary<string, object?>
                {
                    ["allowedProviders"] = new[] { "openai" }
                });

            // Act
            var result = await policy.ExecuteAsync(context);

            // Assert
            var outcome = GetOutcome(result);

            Assert.True(outcome.IsAllowed);
        }

        /// <summary>
        /// Creates a provider admission policy context.
        /// </summary>
        private static AiConcurrencyPolicyContext CreateContext(
            string? provider,
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
                    Model = "gpt-4.1",
                    Operation = "llm.chat"
                },
                Config = config
            };
        }

        /// <summary>
        /// Extracts the strongly typed concurrency policy outcome from a policy result.
        /// </summary>
        private static AiConcurrencyPolicyOutcome GetOutcome(
            AiPolicyResult result)
        {
            var typedResult = Assert.IsType<AiPolicyResultGeneric<AiConcurrencyPolicyOutcome>>(result);

            Assert.NotNull(typedResult.Data);

            return typedResult.Data;
        }
    }
}