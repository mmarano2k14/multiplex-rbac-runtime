using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Executes a single step of a provider-neutral AI pipeline.
    ///
    /// Architecture summary:
    /// AiExecutionEngine
    ///     -> IAiPipelineExecutor
    ///         -> PrepareAsync(...)
    ///             -> IAiPipelineDefinitionSourceSelector
    ///             -> IAiPipelineDefinitionProvider
    ///             -> IAiPipelineResolver
    ///         -> ExecuteNextAsync(...)
    ///             -> IAiStepExecutor
    ///
    /// Responsibilities:
    /// - Select the appropriate pipeline definition provider during preparation
    /// - Resolve the configured pipeline into a runtime executable pipeline
    /// - Determine the current step from the orchestration record
    /// - Execute exactly one resolved step
    /// - Return structured progression data to the execution engine
    ///
    /// This class does not own:
    /// - RBAC context loading
    /// - RBAC context rotation
    /// - optimistic concurrency
    /// - persistence orchestration
    /// </summary>
    public sealed class AiPipelineExecutor : IAiPipelineExecutor
    {
        private readonly IAiPipelineDefinitionSourceSelector _sourceSelector;
        private readonly IAiPipelineResolver _pipelineResolver;
        private readonly IAiStepExecutor _stepExecutor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPipelineExecutor"/> class.
        /// </summary>
        /// <param name="sourceSelector">The selector used to choose the pipeline definition provider.</param>
        /// <param name="pipelineResolver">The resolver used to convert definitions into executable runtime pipelines.</param>
        /// <param name="stepExecutor">The executor used to execute a single runtime step.</param>
        public AiPipelineExecutor(
            IAiPipelineDefinitionSourceSelector sourceSelector,
            IAiPipelineResolver pipelineResolver,
            IAiStepExecutor stepExecutor)
        {
            ArgumentNullException.ThrowIfNull(sourceSelector);
            ArgumentNullException.ThrowIfNull(pipelineResolver);
            ArgumentNullException.ThrowIfNull(stepExecutor);

            _sourceSelector = sourceSelector;
            _pipelineResolver = pipelineResolver;
            _stepExecutor = stepExecutor;
        }

        /// <summary>
        /// Resolves the specified pipeline into an executable runtime pipeline.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The resolved executable pipeline.</returns>
        public async Task<ResolvedAiPipeline> PrepareAsync(
            string pipelineName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            var provider = _sourceSelector.Select(pipelineName);

            var definition = await provider.GetDefinitionAsync(
                pipelineName,
                cancellationToken);

            var pipeline = await _pipelineResolver.ResolveAsync(
                definition,
                cancellationToken);

            var orderedSteps = pipeline.Steps
                .OrderBy(x => x.Order)
                .ToArray();

            if (orderedSteps.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' does not contain any resolved steps.");
            }

            return new ResolvedAiPipeline
            {
                Name = pipeline.Name,
                Version = pipeline.Version,
                Steps = orderedSteps
            };
        }

        /// <summary>
        /// Executes the current pipeline step for the supplied execution context
        /// using the provided resolved pipeline.
        /// </summary>
        /// <param name="pipeline">The resolved executable pipeline.</param>
        /// <param name="context">The shared execution context for the current step.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The pipeline execution result for the executed step.</returns>
        public async Task<PipelineExecutionResult> ExecuteNextAsync(
            ResolvedAiPipeline pipeline,
            AiExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(context);

            var record = context.Record;

            var orderedSteps = pipeline.Steps
                .OrderBy(x => x.Order)
                .ToArray();

            if (record.CurrentStepIndex < 0 || record.CurrentStepIndex > orderedSteps.Length)
            {
                throw new InvalidOperationException(
                    $"Execution '{record.ExecutionId}' has an invalid current step index '{record.CurrentStepIndex}'.");
            }

            if (record.CurrentStepIndex >= orderedSteps.Length)
            {
                throw new InvalidOperationException(
                    $"Execution '{record.ExecutionId}' has no remaining step to execute.");
            }

            var resolvedStep = orderedSteps[record.CurrentStepIndex];

            var stepResult = await _stepExecutor.ExecuteAsync(
                resolvedStep,
                context,
                cancellationToken);

            var nextStepIndex = record.CurrentStepIndex + 1;
            var isCompleted = nextStepIndex >= orderedSteps.Length;
            var nextStepName = isCompleted
                ? null
                : orderedSteps[nextStepIndex].Name;

            return new PipelineExecutionResult
            {
                PipelineName = pipeline.Name,
                Steps = orderedSteps.Select(x => x.Name).ToArray(),
                ExecutedStepName = resolvedStep.Name,
                ExecutedStepIndex = record.CurrentStepIndex,
                NextStepIndex = nextStepIndex,
                NextStepName = nextStepName,
                IsCompleted = isCompleted,
                StepResult = stepResult
            };
        }
    }
}