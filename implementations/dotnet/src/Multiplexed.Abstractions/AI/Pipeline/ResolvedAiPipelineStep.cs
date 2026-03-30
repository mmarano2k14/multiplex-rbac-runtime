using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Represents a resolved executable pipeline step.
    /// This model bridges the declarative pipeline definition and the
    /// concrete runtime step instance resolved by the runtime.
    ///
    /// In sequential mode, <see cref="Order"/> can be used to determine
    /// the next step to execute.
    ///
    /// In DAG mode, step execution should be determined from dependency
    /// satisfaction through <see cref="DependsOn"/>, not from <see cref="Order"/> alone.
    /// </summary>
    public sealed class ResolvedAiPipelineStep
    {
        /// <summary>
        /// Gets or sets the unique step instance name inside the pipeline.
        /// This value must be unique per pipeline and is used as the runtime
        /// identity for state, bindings, and result resolution.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the declarative step key used for registry resolution.
        /// This identifies the step type, not the pipeline instance.
        /// </summary>
        public string StepKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolved runtime step instance.
        /// </summary>
        public IAiStep Step { get; init; } = default!;

        /// <summary>
        /// Gets or sets the execution order of the step.
        ///
        /// This property remains useful for sequential execution and as a stable
        /// deterministic ordering hint, but it is not sufficient on its own for DAG execution.
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// Gets or sets the names of upstream step instances that must complete
        /// before this step can be executed in DAG mode.
        ///
        /// An empty collection means the step has no dependencies and may be considered
        /// a root step in the execution graph.
        /// </summary>
        public IReadOnlyCollection<string> DependsOn { get; init; }
            = Array.Empty<string>();

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

        /// <summary>
        /// Gets a value indicating whether this step has any declared dependencies.
        /// </summary>
        public bool HasDependencies => DependsOn.Count > 0;
    }
}