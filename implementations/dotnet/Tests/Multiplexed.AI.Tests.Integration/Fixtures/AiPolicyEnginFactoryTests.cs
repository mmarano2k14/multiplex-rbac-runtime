using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.AI.Policies;
using Multiplexed.AI.Runtime.AI.Retry;
using Multiplexed.AI.Runtime.AI.Retry.Policies;
using System;
using System.Collections.Generic;
using System.Text;

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

            return new DefaultAiPolicyEngineFactory(
                policyRegistry,
                policyEngineRegistry);
        }
    }
}
