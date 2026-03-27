namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Represents a single declarative step inside an AI pipeline definition.
    /// The step is identified by a provider-neutral key so that the definition
    /// remains portable across storage providers and runtime implementations.
    /// </summary>
    public sealed class AiPipelineStepDefinition
    {
        /// <summary>
        /// Gets or sets the unique declarative step name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider-neutral runtime step key used to resolve
        /// the executable step implementation.
        /// </summary>
        public string StepKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the execution order of the step.
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// Gets or sets the optional declarative input for this step.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Input { get; init; }
            = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets the optional declarative configuration for this step.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Config { get; init; }
            = new Dictionary<string, object?>();
    }
}