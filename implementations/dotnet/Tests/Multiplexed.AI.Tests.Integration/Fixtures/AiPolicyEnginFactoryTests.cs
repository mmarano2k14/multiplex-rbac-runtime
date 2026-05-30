using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry.Policies;
using static Multiplexed.AI.Tests.Integration.Helpers.MetricsFactory;

namespace Multiplexed.AI.Tests.Integration.Fixtures
{
    public class AiPolicyEnginFactoryTests
    {
        public static IAiPolicyEngineFactory CreatePolicyEngineFactory()
        {
            var policies = new IAiPolicy[]
            {
                new DefaultTransientRetryPolicy(),
                new DefaultTimeoutRetryPolicy(),
                new DefaultRateLimitRetryPolicy()
            };

            var policyRegistry = new DefaultAiPolicyRegistry(policies);

            var policyEngineRegistry = new DefaultAiPolicyEngineRegistry(
                new[]
                {
                    typeof(DefaultAiRetryEngine)
                });

            IAiRuntimeObservability observability = ObservabilityFactory.Create();

            return new DefaultAiPolicyEngineFactory(
                policyRegistry,
                policyEngineRegistry,
                observability);
        }
    }
}