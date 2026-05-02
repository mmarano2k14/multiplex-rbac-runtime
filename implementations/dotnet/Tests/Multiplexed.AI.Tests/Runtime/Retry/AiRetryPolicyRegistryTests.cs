using Microsoft.Extensions.DependencyInjection;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.AI.Retry;

namespace Multiplexed.AI.Tests.Runtime.AI.Retry
{
    public sealed class AiRetryPolicyRegistryTests
    {
        [Fact]
        public void Resolve_Should_Return_Default_Transient_Retry_Policy()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAiRetryScheduler, DefaultAiRetryScheduler>();

            services.AddSingleton<DefaultTransientRetryPolicy>();
            services.AddSingleton<IAiPolicy>(sp => sp.GetRequiredService<DefaultTransientRetryPolicy>());
            services.AddSingleton<IAiRetryPolicy>(sp => sp.GetRequiredService<DefaultTransientRetryPolicy>());

            services.AddSingleton<IAiPolicyRegistry, DefaultAiPolicyRegistry>();
            services.AddSingleton<IAiRetryPolicyResolver, DefaultAiRetryPolicyResolver>();

            var provider = services.BuildServiceProvider();

            var resolver = provider.GetRequiredService<IAiRetryPolicyResolver>();

            var policy = resolver.Resolve("retry.transient.default");

            Assert.NotNull(policy);
            Assert.Equal("retry.transient.default", policy.Key);
            Assert.Equal(AiPolicyKind.Retry, policy.Kind);
        }

        [Fact]
        public async Task Default_Decision_Service_Should_Schedule_Retry()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAiRetryScheduler, DefaultAiRetryScheduler>();
            services.AddSingleton<IAiRetryClassifier, DefaultAiRetryClassifier>();

            services.AddSingleton<DefaultTransientRetryPolicy>();
            services.AddSingleton<IAiPolicy>(sp => sp.GetRequiredService<DefaultTransientRetryPolicy>());
            services.AddSingleton<IAiRetryPolicy>(sp => sp.GetRequiredService<DefaultTransientRetryPolicy>());

            services.AddSingleton<IAiPolicyRegistry, DefaultAiPolicyRegistry>();
            services.AddSingleton<IAiRetryPolicyResolver, DefaultAiRetryPolicyResolver>();
            services.AddSingleton<IAiRetryDecisionService, DefaultAiRetryDecisionService>();

            var provider = services.BuildServiceProvider();

            var decisionService = provider.GetRequiredService<IAiRetryDecisionService>();

            var decision = await decisionService.DecideAsync(
                new AiRetryContext
                {
                    ExecutionId = "exec-1",
                    StepId = "compose",
                    StepKey = "rag.compose",
                    RetryCount = 0,
                    MaxRetries = 5,
                    Exception = new InvalidOperationException("Transient failure."),
                    FailureReason = "Transient failure.",
                    FailedAtUtc = DateTimeOffset.UtcNow,
                    Retry = new AiRetryPolicyDefinition
                    {
                        Policies = new[] { "retry.transient.default" },
                        MaxRetries = 5,
                        Strategy = AiRetryBackoffStrategy.Exponential,
                        BaseDelayMs = 200,
                        MaxDelayMs = 5000,
                        Jitter = false
                    }
                });

            Assert.Equal(AiRetryDecisionKind.RetryScheduled, decision.Kind);
            Assert.True(decision.Retryable);
            Assert.Equal("retry.transient.default", decision.PolicyKey);
            Assert.Equal(TimeSpan.FromMilliseconds(200), decision.Delay);
        }
    }
}