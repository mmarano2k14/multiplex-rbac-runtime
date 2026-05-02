using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI.Retry.old
{
    /// <summary>
    /// Defines the delay calculation strategy used between retry attempts.
    /// </summary>
    public enum AiRetryBackoffMode
    {
        Fixed = 0,
        Exponential = 1
    }
}
