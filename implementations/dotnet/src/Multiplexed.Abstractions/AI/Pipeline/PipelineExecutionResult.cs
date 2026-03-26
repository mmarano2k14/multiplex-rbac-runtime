using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Represents the result of executing a single pipeline step.
    ///
    /// The execution engine uses this result to:
    /// - merge state updates
    /// - advance the current step
    /// - mark the execution as completed when required
    /// - persist the transition safely
    /// </summary>
    public sealed class PipelineExecutionResult
    {
        /// <summary>
        /// Gets or sets the pipeline name.
        /// </summary>
        public string PipelineName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the ordered pipeline step names.
        /// </summary>
        public IReadOnlyCollection<string> Steps { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the name of the executed step.
        /// </summary>
        public string ExecutedStepName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the zero-based index of the executed step.
        /// </summary>
        public int ExecutedStepIndex { get; init; }

        /// <summary>
        /// Gets or sets the zero-based index of the next step.
        /// </summary>
        public int NextStepIndex { get; init; }

        /// <summary>
        /// Gets or sets the next step name if one exists.
        /// </summary>
        public string? NextStepName { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether the pipeline is completed.
        /// </summary>
        public bool IsCompleted { get; init; }

        /// <summary>
        /// Gets or sets the raw step execution result.
        /// </summary>
        public AiStepResult StepResult { get; init; } = default!;
    }
}