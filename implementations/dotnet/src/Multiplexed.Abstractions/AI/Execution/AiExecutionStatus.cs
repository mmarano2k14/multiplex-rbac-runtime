namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the lifecycle status of an AI execution.
    /// </summary>
    public enum AiExecutionStatus
    {
        Pending = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }
}