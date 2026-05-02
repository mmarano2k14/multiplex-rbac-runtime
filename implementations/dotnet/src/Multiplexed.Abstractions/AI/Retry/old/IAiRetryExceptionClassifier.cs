using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI.Retry.old
{
    /// <summary>
    /// Classifies exceptions to determine whether they are retryable.
    /// This abstraction allows provider-specific or infrastructure-specific policies
    /// to be plugged into the runtime without coupling retry logic to step code.
    /// </summary>
    public interface IAiRetryExceptionClassifier
    {
        /// <summary>
        /// Returns true when the exception is considered transient and may be retried.
        /// </summary>
        bool IsRetryable(Exception exception);
    }
}
