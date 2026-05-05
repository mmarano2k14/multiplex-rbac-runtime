namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Defines the retention policy configuration resolved by the retention engine.
    /// </summary>
    public sealed class AiRetentionPolicyDefinition
    {
        /// <summary>
        /// Gets the ordered retention policy keys to evaluate.
        /// </summary>
        public IReadOnlyCollection<string> Policies { get; init; } = [];
    }
}