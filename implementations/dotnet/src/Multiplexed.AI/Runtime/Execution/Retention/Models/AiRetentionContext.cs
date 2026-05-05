using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Represents the context evaluated by retention policies.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Provide retention policies with the execution state and runtime evaluation metadata.
    /// - Carry the resolved trigger definition so policies can make step-level decisions.
    ///
    /// DESIGN:
    /// - This context is immutable from the perspective of policies.
    /// - It is created by the retention engine after resolving pipeline and step configuration.
    /// - Policies must not mutate the execution state directly.
    /// </remarks>
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
        /// Gets the resolved retention trigger definition.
        /// </summary>
        /// <remarks>
        /// Policies may use this value to filter candidate steps based on per-step pressure,
        /// such as <see cref="AiStepState.InlinePayloadSizeBytes"/>.
        /// </remarks>
        public AiRetentionTriggerDefinition Trigger { get; init; } = new();

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