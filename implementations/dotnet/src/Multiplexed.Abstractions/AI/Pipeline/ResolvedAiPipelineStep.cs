using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Represents a resolved executable pipeline step.
    /// This model bridges the declarative pipeline definition and the
    /// concrete runtime step instance resolved by the runtime.
    /// </summary>
    public sealed class ResolvedAiPipelineStep
    {
        /// <summary>
        /// Gets or sets the unique step name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the declarative step key used for resolution.
        /// </summary>
        public string StepKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolved runtime step instance.
        /// </summary>
        public IAiStep Step { get; init; } = default!;

        /// <summary>
        /// Gets or sets the execution order of the step.
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// Gets or sets the optional declarative input for this step instance.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Input { get; init; }
            = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets the optional declarative configuration for this step instance.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Config { get; init; }
            = new Dictionary<string, object?>();
    }
}