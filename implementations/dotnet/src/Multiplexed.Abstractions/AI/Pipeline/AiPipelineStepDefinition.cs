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
        ///
        /// This is the step instance identity within the pipeline.
        /// It must be unique per pipeline definition.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider-neutral runtime step key used to resolve
        /// the executable step implementation.
        ///
        /// This identifies the step type, not the step instance.
        /// </summary>
        public string StepKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the execution order of the step.
        ///
        /// This property is primarily used for sequential execution and as a stable
        /// deterministic ordering hint for DAG scheduling.
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// Gets or sets the names of upstream step instances that must complete
        /// before this step can be executed in DAG mode.
        ///
        /// This collection must contain pipeline step names, not step type keys.
        /// An empty collection means the step has no dependencies and can be treated
        /// as a root node in the execution graph.
        /// </summary>
        public IReadOnlyCollection<string> DependsOn { get; init; }
            = Array.Empty<string>();

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

        /// <summary>
        /// Gets a value indicating whether this step declares dependencies.
        /// </summary>
        public bool HasDependencies => DependsOn.Count > 0;
    }
}