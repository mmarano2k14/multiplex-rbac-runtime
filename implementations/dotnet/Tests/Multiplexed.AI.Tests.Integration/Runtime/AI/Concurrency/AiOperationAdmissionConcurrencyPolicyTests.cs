using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.AI.Concurrency.Policies;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.AI.Concurrency
{
    /// <summary>
    /// Provides focused coverage for <see cref="AiOperationAdmissionConcurrencyPolicy"/>.
    /// </summary>
    public sealed class AiOperationAdmissionConcurrencyPolicyTests
    {
        /// <summary>
        /// Verifies that the policy allows execution when the operation is allowed.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Allow_When_Operation_Is_Allowed()
        {
            var policy = new AiOperationAdmissionConcurrencyPolicy();

            var context = CreateContext(
                operation: "llm.chat",
                config: new Dictionary<string, object?>
                {
                    ["allowedOperations"] = new[] { "llm.chat", "rag.retrieve" }
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.True(outcome.IsAllowed);
            Assert.Null(outcome.Reason);
            Assert.Null(outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that the policy denies execution when the operation is blocked.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Deny_When_Operation_Is_Blocked()
        {
            var policy = new AiOperationAdmissionConcurrencyPolicy();

            var context = CreateContext(
                operation: "tool.dangerous",
                config: new Dictionary<string, object?>
                {
                    ["blockedOperations"] = new[] { "tool.dangerous" },
                    ["retryAfterMs"] = 500
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.False(outcome.IsAllowed);
            Assert.Contains("blocked", outcome.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TimeSpan.FromMilliseconds(500), outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that the policy denies execution when an operation is required but missing.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Deny_When_Operation_Is_Required_But_Missing()
        {
            var policy = new AiOperationAdmissionConcurrencyPolicy();

            var context = CreateContext(
                operation: null,
                config: new Dictionary<string, object?>
                {
                    ["requireOperation"] = true,
                    ["reason"] = "Operation is required for this pipeline.",
                    ["retryAfterMs"] = 250
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.False(outcome.IsAllowed);
            Assert.Equal("Operation is required for this pipeline.", outcome.Reason);
            Assert.Equal(TimeSpan.FromMilliseconds(250), outcome.RetryAfter);
        }

        /// <summary>
        /// Verifies that operation matching is case-insensitive.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Compare_Operations_Case_Insensitively()
        {
            var policy = new AiOperationAdmissionConcurrencyPolicy();

            var context = CreateContext(
                operation: "LLM.Chat",
                config: new Dictionary<string, object?>
                {
                    ["allowedOperations"] = new[] { "llm.chat" }
                });

            var result = await policy.ExecuteAsync(context);

            var outcome = GetOutcome(result);

            Assert.True(outcome.IsAllowed);
        }

        /// <summary>
        /// Creates an operation admission policy context.
        /// </summary>
        private static AiConcurrencyPolicyContext CreateContext(
            string? operation,
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
                    Provider = "openai",
                    Model = "gpt-4.1",
                    Operation = operation
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