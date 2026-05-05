using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Represents the execution context evaluated by retention policies.
    /// </summary>
    public sealed class AiRetentionContext
    {
        /// <summary>
        /// Gets the execution identifier.
        /// </summary>
        public string ExecutionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the execution state evaluated for retention.
        /// </summary>
        public AiExecutionState ExecutionState { get; init; } = default!;

        /// <summary>
        /// Gets the current UTC timestamp used during retention evaluation.
        /// </summary>
        public DateTime UtcNow { get; init; }

        /// <summary>
        /// Gets additional metadata available to retention policies.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Metadata { get; init; }
            = new Dictionary<string, object?>();
    }
}