using Multiplexed.Abstractions.AI.Policies;
using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Defines retry behavior configured for an AI pipeline step.
    /// </summary>
    /// <remarks>
    /// This model is typically populated from a step configuration section such as
    /// <c>config.retry</c>. It describes which retry policies should be evaluated,
    /// how many retry attempts are allowed, and how retry delays should be computed.
    /// </remarks>
    public sealed class AiRetryPolicyDefinition
    {
        /// <summary>
        /// Gets the ordered policy keys used to evaluate retry behavior.
        /// </summary>
        /// <remarks>
        /// The runtime evaluates these policies in order. Each key should correspond
        /// to a registered policy decorated with an AI policy attribute.
        /// </remarks>
        public List<AiConfiguredPolicyDefinition> Policies { get; set; } = new();

        /// <summary>
        /// Gets the maximum number of retry attempts allowed for the step.
        /// </summary>
        public int MaxRetries { get; init; } = 3;

        /// <summary>
        /// Gets the strategy used to compute the delay between retry attempts.
        /// </summary>
        public AiRetryBackoffStrategy Strategy { get; init; } = AiRetryBackoffStrategy.Fixed;

        /// <summary>
        /// Gets the base delay, in milliseconds, used by the selected backoff strategy.
        /// </summary>
        public int BaseDelayMs { get; init; } = 500;

        /// <summary>
        /// Gets the optional maximum delay, in milliseconds, applied after backoff calculation.
        /// </summary>
        public int? MaxDelayMs { get; init; }

        /// <summary>
        /// Gets a value indicating whether bounded jitter should be applied to computed retry delays.
        /// </summary>
        public bool Jitter { get; init; }
    }
}