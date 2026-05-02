using System.Collections.Generic;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Resolves retry policies from stable retry policy keys.
    /// </summary>
    /// <remarks>
    /// Policy keys are typically declared in step configuration under <c>config.retry.policies</c>
    /// and are resolved against policies registered with the runtime dependency container.
    /// </remarks>
    public interface IAiRetryPolicyResolver
    {
        /// <summary>
        /// Resolves a retry policy by its stable key.
        /// </summary>
        /// <param name="key">The retry policy key to resolve.</param>
        /// <returns>The retry policy matching the specified key.</returns>
        IAiRetryPolicy Resolve(string key);

        /// <summary>
        /// Resolves multiple retry policies in the same order as the provided keys.
        /// </summary>
        /// <param name="keys">The ordered retry policy keys to resolve.</param>
        /// <returns>The resolved retry policies.</returns>
        IReadOnlyList<IAiRetryPolicy> ResolveMany(IEnumerable<string> keys);
    }
}