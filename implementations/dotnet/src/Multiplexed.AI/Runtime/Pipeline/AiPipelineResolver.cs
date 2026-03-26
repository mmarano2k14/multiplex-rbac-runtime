using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Resolves provider-neutral AI pipeline definitions into executable runtime plans.
    ///
    /// Architecture summary:
    /// Pipeline Definition Provider -> Pipeline Definition -> Pipeline Resolver -> Resolved Pipeline -> Execution Engine -> Step Executor
    ///
    /// Responsibilities:
    /// - Validate the declarative pipeline definition
    /// - Resolve runtime steps from portable step keys
    /// - Produce an ordered executable pipeline plan
    ///
    /// This separation ensures that pipeline definitions remain portable across
    /// providers such as in-memory, JSON, and database-backed implementations,
    /// while the runtime stays responsible for resolving and executing concrete steps.
    /// </summary>
    public sealed class AiPipelineResolver : IAiPipelineResolver
    {
        private readonly IAiStepRegistry _stepRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPipelineResolver"/> class.
        /// </summary>
        /// <param name="stepRegistry">
        /// The registry used to resolve runtime step instances from declarative step keys.
        /// </param>
        public AiPipelineResolver(IAiStepRegistry stepRegistry)
        {
            ArgumentNullException.ThrowIfNull(stepRegistry);

            _stepRegistry = stepRegistry;
        }

        /// <summary>
        /// Resolves the specified pipeline definition into an executable runtime plan.
        /// </summary>
        /// <param name="definition">The pipeline definition to resolve.</param>
        /// <param name="cancellationToken">The cancellation token for the active operation.</param>
        /// <returns>The resolved executable pipeline plan.</returns>
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

            var resolvedSteps = new List<ResolvedAiPipelineStep>();

            foreach (var stepDefinition in definition.Steps.OrderBy(x => x.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(stepDefinition.Name))
                {
                    throw new InvalidOperationException(
                        "Pipeline step name cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(stepDefinition.StepKey))
                {
                    throw new InvalidOperationException(
                        $"Pipeline step '{stepDefinition.Name}' does not define a valid step key.");
                }

                var step = _stepRegistry.Resolve(stepDefinition.StepKey);

                resolvedSteps.Add(new ResolvedAiPipelineStep
                {
                    Name = stepDefinition.Name,
                    StepKey = stepDefinition.StepKey,
                    Step = step,
                    Order = stepDefinition.Order
                });
            }

            var resolvedPipeline = new ResolvedAiPipeline
            {
                Name = definition.Name,
                Version = definition.Version,
                Steps = resolvedSteps
                    .OrderBy(x => x.Order)
                    .ToArray()
            };

            return Task.FromResult(resolvedPipeline);
        }
    }
}