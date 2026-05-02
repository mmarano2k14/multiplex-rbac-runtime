using Multiplexed.Abstractions.AI.Retry.old;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Default retry classifier for common transient infrastructure failures.
    /// This implementation is intentionally conservative and can be replaced
    /// with provider-specific logic when needed.
    /// </summary>
    public sealed class DefaultAiRetryExceptionClassifier : IAiRetryExceptionClassifier
    {
        public bool IsRetryable(Exception exception)
        {
            return exception switch
            {
                TimeoutException => true,
                HttpRequestException => true,
                TaskCanceledException => true,
                _ => false
            };
        }
    }
}
