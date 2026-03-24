using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Runtime;

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
        private const string InputKey = "input";
        private const string SummaryKey = "summary";

        private readonly AiStepRunner _runner;
        private readonly IRuntimeEventContext _realtime;
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPipelineService"/> class.
        /// </summary>
        /// <param name="runner">The step runner responsible for sequential execution.</param>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        /// <param name="services">The service provider used by the execution context.</param>
        public AiPipelineService(
            AiStepRunner runner,
            IRuntimeEventContext realtime,
            IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(realtime);
            ArgumentNullException.ThrowIfNull(services);

            _runner = runner;
            _realtime = realtime;
            _services = services;
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
            context.Set(InputKey, input);

            LogPipelineRequested(context);

            await _runner.RunAsync(context, cancellationToken);

            var result = context.Get<string>(SummaryKey);

            LogPipelineCompleted(context, result);

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
                Status = "Running",
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

        /// <summary>
        /// Emits a structured event when pipeline execution is requested.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        private void LogPipelineRequested(AiExecutionContext context)
        {
            _realtime.LogInfo(
                message: "AI pipeline service execution requested.",
                category: "ai.pipeline.request",
                data: new
                {
                    context.ExecutionId
                });
        }

        /// <summary>
        /// Emits a structured event when pipeline execution completes.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <param name="result">The final pipeline result.</param>
        private void LogPipelineCompleted(AiExecutionContext context, string? result)
        {
            _realtime.LogInfo(
                message: "AI pipeline service execution completed.",
                category: "ai.pipeline.result",
                data: new
                {
                    context.ExecutionId,
                    Result = result
                });
        }
    }
}