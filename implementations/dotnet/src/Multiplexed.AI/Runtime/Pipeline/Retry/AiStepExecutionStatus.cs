using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Represents the current lifecycle status of a step execution.
    /// </summary>
    public enum AiStepExecutionStatus
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3
    }
}
