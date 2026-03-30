namespace Multiplexed.AI.Stores
{
    /// <summary>
    /// Represents a step successfully claimed by a worker.
    /// </summary>
    public sealed class ClaimedAiStep
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string ClaimToken { get; set; } = string.Empty;
    }
}