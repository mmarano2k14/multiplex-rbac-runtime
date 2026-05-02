using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI.Retry.old
{
    /// <summary>
    /// Declares retry behavior for an AI step.
    /// This attribute keeps retry intent close to the step definition while leaving
    /// execution responsibility to the runtime pipeline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class AiRetryPolicyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new retry policy attribute.
        /// </summary>
        /// <param name="maxRetries">
        /// The maximum number of retry attempts after the initial execution failure.
        /// </param>
        /// <param name="delayMilliseconds">
        /// The base delay, in milliseconds, used before the next retry.
        /// </param>
        public AiRetryPolicyAttribute(int maxRetries = 3, int delayMilliseconds = 1000)
        {
            if (maxRetries < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries));
            }

            if (delayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
            }

            MaxRetries = maxRetries;
            DelayMilliseconds = delayMilliseconds;
        }

        /// <summary>
        /// Gets the maximum number of retry attempts after a failed execution.
        /// </summary>
        public int MaxRetries { get; }

        /// <summary>
        /// Gets the base delay, in milliseconds, used for backoff calculation.
        /// </summary>
        public int DelayMilliseconds { get; }

        /// <summary>
        /// Gets or sets the backoff mode.
        /// </summary>
        public AiRetryBackoffMode BackoffMode { get; init; } = AiRetryBackoffMode.Exponential;

        /// <summary>
        /// Gets or sets a value indicating whether the runtime should only retry
        /// exceptions classified as transient.
        /// </summary>
        public bool RetryTransientOnly { get; init; } = true;
    }
}
