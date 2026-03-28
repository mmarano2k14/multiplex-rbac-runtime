using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the execution state of a single pipeline step.
    ///
    /// This object captures:
    /// - Resolved inputs for the step
    /// - Produced outputs
    /// - Execution timing information
    ///
    /// It enables traceability, replay, and debugging.
    /// </summary>
    public sealed class AiStepState
    {
        /// <summary>
        /// Gets or sets the step name.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolved inputs for this step.
        /// These are the actual values used during execution.
        /// </summary>
        public Dictionary<string, object?> Inputs { get; set; }
            = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the Result produced by this step.
        /// </summary>
        public AiStepResult? Result { get; set; }

        /// <summary>
        /// Gets or sets the optional declarative configuration for this step instance.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Config { get; set; }
            = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets the UTC timestamp when the step state was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp when the step state was last updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}