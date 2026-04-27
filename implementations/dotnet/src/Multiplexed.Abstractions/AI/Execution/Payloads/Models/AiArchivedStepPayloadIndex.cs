using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;

namespace Multiplexed.Abstractions.AI.Execution.Payloads.Models
{
    /// <summary>
    /// Represents an index entry for an archived / evicted step state.
    ///
    /// PURPOSE:
    /// - Records that a step existed and was moved out of hot execution state.
    /// - Stores the external payload reference needed to reload the step.
    /// - Preserves terminal step status for dependency, convergence, and diagnostics.
    ///
    /// IMPORTANT:
    /// - This object is an index entry only.
    /// - It does not contain the full step state.
    /// - Full step state is loaded through <see cref="IAiStepPayloadStore"/>.
    /// </summary>
    public sealed class AiArchivedStepPayloadIndex
    {
        /// <summary>
        /// Gets or sets the execution id that owns this archived step.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the archived step name.
        /// </summary>
        public string StepName { get; set; } = default!;

        /// <summary>
        /// Gets or sets the terminal status captured at archive time.
        /// </summary>
        public AiStepExecutionStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the external payload reference for the serialized step state.
        /// </summary>
        public AiStoredPayload Payload { get; set; } = default!;

        /// <summary>
        /// Gets or sets when the step was archived.
        /// </summary>
        public DateTime ArchivedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets why the step was archived.
        /// </summary>
        public string Reason { get; set; } = "retention";
    }
}