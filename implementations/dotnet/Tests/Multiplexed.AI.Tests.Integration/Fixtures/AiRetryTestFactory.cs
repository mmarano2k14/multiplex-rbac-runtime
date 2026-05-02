using Microsoft.Extensions.DependencyInjection;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.AI.Retry;

namespace Multiplexed.AI.Tests.Fixtures
{
    internal static class AiRetryTestFactory
    {
        public static RetryExecutionAdapter CreateRetryAdapter()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAiPolicyRegistry, DefaultAiPolicyRegistry>();

            services.AddSingleton<IAiRetryScheduler, DefaultAiRetryScheduler>();
            services.AddSingleton<IAiRetryClassifier, DefaultAiRetryClassifier>();
            services.AddSingleton<IAiRetryPolicyResolver, DefaultAiRetryPolicyResolver>();
            services.AddSingleton<IAiRetryDecisionService, DefaultAiRetryDecisionService>();
            services.AddSingleton<IAiRetryPolicyDefinitionResolver, DefaultAiRetryPolicyDefinitionResolver>();

            services.AddSingleton<DefaultTransientRetryPolicy>();
            services.AddSingleton<IAiPolicy>(sp => sp.GetRequiredService<DefaultTransientRetryPolicy>());
            services.AddSingleton<IAiRetryPolicy>(sp => sp.GetRequiredService<DefaultTransientRetryPolicy>());

            services.AddSingleton<RetryExecutionAdapter>();

            return services
                .BuildServiceProvider()
                .GetRequiredService<RetryExecutionAdapter>();
        }

        public static IAiRetryPolicyDefinitionResolver CreateRetryDefinitionResolver()
        {
            return new DefaultAiRetryPolicyDefinitionResolver();
        }
    }
}