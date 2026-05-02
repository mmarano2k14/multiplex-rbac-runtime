using System;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Resolves AI retry policies from stable policy keys.
    /// </summary>
    /// <remarks>
    /// This resolver delegates policy lookup to the shared AI policy registry and
    /// restricts resolution to policies registered with <see cref="AiPolicyKind.Retry"/>.
    /// </remarks>
    public sealed class DefaultAiRetryPolicyResolver : IAiRetryPolicyResolver
    {
        private readonly IAiPolicyRegistry policyRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetryPolicyResolver"/> class.
        /// </summary>
        /// <param name="policyRegistry">
        /// The shared AI policy registry used to resolve policies by key and kind.
        /// </param>
        public DefaultAiRetryPolicyResolver(IAiPolicyRegistry policyRegistry)
        {
            this.policyRegistry = policyRegistry ?? throw new ArgumentNullException(nameof(policyRegistry));
        }

        /// <inheritdoc />
        public IAiRetryPolicy Resolve(string key)
        {
            var policy = policyRegistry.Resolve(key, AiPolicyKind.Retry);

            if (policy is not IAiRetryPolicy retryPolicy)
            {
                throw new InvalidOperationException(
                    $"AI policy '{key}' is registered as retry policy but does not implement {nameof(IAiRetryPolicy)}.");
            }

            return retryPolicy;
        }

        /// <inheritdoc />
        public IReadOnlyList<IAiRetryPolicy> ResolveMany(IEnumerable<string> keys)
        {
            if (keys is null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            return keys.Select(Resolve).ToArray();
        }
    }
}