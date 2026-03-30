using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Resolves provider-neutral AI pipeline definitions into executable runtime plans.
    ///
    /// Architecture:
    /// Definition (JSON / DB / Memory)
    ///     → Resolver
    ///     → Resolved Pipeline
    ///     → Execution Engine
    ///     → Step Executor
    ///
    /// Responsibilities:
    /// - Validate pipeline structure (names, dependencies, cycles)
    /// - Resolve runtime step instances from StepKey
    /// - Produce a deterministic, executable pipeline representation
    ///
    /// IMPORTANT:
    /// This layer is responsible for structural correctness.
    /// No invalid pipeline should reach the execution engine.
    /// </summary>
    public sealed class AiPipelineResolver : IAiPipelineResolver
    {
        private readonly IAiStepRegistry _stepRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPipelineResolver"/> class.
        /// </summary>
        /// <param name="stepRegistry">
        /// Registry used to resolve runtime step implementations.
        /// </param>
        public AiPipelineResolver(IAiStepRegistry stepRegistry)
        {
            ArgumentNullException.ThrowIfNull(stepRegistry);
            _stepRegistry = stepRegistry;
        }

        /// <summary>
        /// Resolves a declarative pipeline definition into an executable pipeline.
        /// </summary>
        public Task<ResolvedAiPipeline> ResolveAsync(
            AiPipelineDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw new ArgumentException(
                    "Pipeline name cannot be null or empty.",
                    nameof(definition));
            }

            if (definition.Steps.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{definition.Name}' does not define any steps.");
            }

            // --- VALIDATION PHASE ---
            ValidateStepDefinitions(definition);
            ValidateAcyclicGraph(definition);

            // --- RESOLUTION PHASE ---
            var resolvedSteps = new List<ResolvedAiPipelineStep>();

            foreach (var stepDefinition in definition.Steps.OrderBy(x => x.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = _stepRegistry.Resolve(stepDefinition.StepKey);

                resolvedSteps.Add(new ResolvedAiPipelineStep
                {
                    Name = stepDefinition.Name,
                    StepKey = stepDefinition.StepKey,
                    Step = step,
                    Order = stepDefinition.Order,
                    DependsOn = stepDefinition.DependsOn,
                    Input = stepDefinition.Input,
                    Config = stepDefinition.Config
                });
            }

            return Task.FromResult(new ResolvedAiPipeline
            {
                Name = definition.Name,
                Version = definition.Version,
                ExecutionMode = definition.ExecutionMode,
                Steps = resolvedSteps.OrderBy(x => x.Order).ToArray()
            });
        }

        /// <summary>
        /// Validates basic step constraints:
        /// - Name must exist
        /// - StepKey must exist
        /// - Names must be unique
        /// - Dependencies must exist
        /// - No self-dependency
        /// </summary>
        private static void ValidateStepDefinitions(AiPipelineDefinition definition)
        {
            var stepsByName = new Dictionary<string, AiPipelineStepDefinition>(StringComparer.Ordinal);

            foreach (var step in definition.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    throw new InvalidOperationException("Pipeline step name cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(step.StepKey))
                {
                    throw new InvalidOperationException(
                        $"Step '{step.Name}' does not define a valid StepKey.");
                }

                if (!stepsByName.TryAdd(step.Name, step))
                {
                    throw new InvalidOperationException(
                        $"Duplicate step name '{step.Name}' detected.");
                }
            }

            foreach (var step in definition.Steps)
            {
                foreach (var dep in step.DependsOn)
                {
                    if (string.IsNullOrWhiteSpace(dep))
                    {
                        throw new InvalidOperationException(
                            $"Step '{step.Name}' contains an empty dependency.");
                    }

                    if (dep == step.Name)
                    {
                        throw new InvalidOperationException(
                            $"Step '{step.Name}' cannot depend on itself.");
                    }

                    if (!stepsByName.ContainsKey(dep))
                    {
                        throw new InvalidOperationException(
                            $"Step '{step.Name}' depends on unknown step '{dep}'.");
                    }
                }
            }
        }

        /// <summary>
        /// Validates that the dependency graph is acyclic (DAG constraint).
        ///
        /// Uses depth-first search (DFS):
        /// - visited: nodes already validated
        /// - visiting: current recursion stack
        ///
        /// If a node is revisited while still in "visiting", a cycle exists.
        /// </summary>
        private static void ValidateAcyclicGraph(AiPipelineDefinition definition)
        {
            var stepsByName = definition.Steps.ToDictionary(x => x.Name, StringComparer.Ordinal);

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);

            foreach (var step in definition.Steps)
            {
                if (!visited.Contains(step.Name))
                {
                    Visit(step.Name, stepsByName, visited, visiting);
                }
            }
        }

        /// <summary>
        /// DFS traversal used for cycle detection.
        /// </summary>
        private static void Visit(
            string stepName,
            IReadOnlyDictionary<string, AiPipelineStepDefinition> stepsByName,
            ISet<string> visited,
            ISet<string> visiting)
        {
            if (visited.Contains(stepName))
                return;

            if (visiting.Contains(stepName))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving step '{stepName}'.");
            }

            visiting.Add(stepName);

            var step = stepsByName[stepName];

            foreach (var dep in step.DependsOn)
            {
                Visit(dep, stepsByName, visited, visiting);
            }

            visiting.Remove(stepName);
            visited.Add(stepName);
        }
    }
}