namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Represents the type of retry decision.
    /// </summary>
    public enum AiRetryDecisionKind
    {
        Retry = 0,
        Fail = 1,
        Stop = 2
    }
}