using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Runtime;
using Multiplexed.AI.Runtime.Logging;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// High-level entry point for executing an in-memory AI pipeline.
    ///
    /// Responsibilities:
    /// - Create a new execution record
    /// - Create and initialize execution state
    /// - Build the shared execution context
    /// - Seed the initial pipeline input
    /// - Delegate execution to the step runner
    /// - Return the final pipeline output
    /// </summary>
    public sealed class AiPipelineService
    {

        private readonly AiStepRunner _runner;
        private readonly IServiceProvider _services;
        private readonly IAiRuntimeLogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPipelineService"/> class.
        /// </summary>
        /// <param name="runner">The step runner responsible for sequential execution.</param>
        /// <param name="services">The service provider used by the execution context.</param>
        /// <param name="logger">The centralized AI runtime logger responsible for structured tracing across engine, pipeline, and step execution.</param>
        public AiPipelineService(
            AiStepRunner runner,
            IServiceProvider services,
            IAiRuntimeLogger logger)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(logger);

            _runner = runner;
            _services = services;
            _logger = logger;
        }

        /// <summary>
        /// Executes the AI pipeline using a simple string input.
        /// </summary>
        /// <param name="input">Input data for the pipeline.</param>
        /// <param name="cancellationToken">The cancellation token for the active execution.</param>
        /// <returns>The final pipeline output.</returns>
        public async Task<string> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));

            var context = CreateExecutionContext(cancellationToken);
            context.Set(AiExecutionKeys.Input, input);

            _logger.PipelineService.ExecutionRequested(context);

            await _runner.RunAsync(context, cancellationToken);

            var result = context.Get<string>(AiExecutionKeys.Summary);

            _logger.PipelineService.ExecutionCompleted(context, result);

            return result ?? string.Empty;
        }

        /// <summary>
        /// Creates a new in-memory execution context for a pipeline run.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the execution.</param>
        /// <returns>A new initialized execution context.</returns>
        private AiExecutionContext CreateExecutionContext(CancellationToken cancellationToken)
        {
            var record = new AiExecutionRecord
            {
                Status = AiExecutionStatus.Running,
                CurrentStep = string.Empty
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId
            };

            return new AiExecutionContext(
                record,
                state,
                _services,
                cancellationToken);
        }
    }
}