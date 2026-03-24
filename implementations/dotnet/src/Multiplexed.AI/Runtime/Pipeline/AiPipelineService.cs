using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// High-level entry point for executing an AI pipeline.
    /// Responsible for:
    /// - Creating and initializing the execution context
    /// - Injecting input data
    /// - Delegating execution to the step runner
    /// - Returning the final result
    /// </summary>
    public sealed class AiPipelineService
    {
        private readonly AiStepRunner _runner;
        private readonly IRuntimeEventContext _realtime;

        public AiPipelineService(
            AiStepRunner runner,
            IRuntimeEventContext realtime)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(realtime);

            _runner = runner;
            _realtime = realtime;
        }

        /// <summary>
        /// Executes the AI pipeline using a simple string input.
        /// </summary>
        /// <param name="input">Input data for the pipeline.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Final pipeline output.</returns>
        public async Task<string> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));

            // Create a new execution context
            var context = new AiStepContext();

            // Inject initial input into context
            context.Set("input", input);

            _realtime.LogInfo(
                message: "AI pipeline service execution requested.",
                category: "ai.pipeline.request",
                data: new
                {
                    context.ExecutionId
                });

            // Execute pipeline
            await _runner.RunAsync(context, cancellationToken);

            // Extract final result from context
            var result = context.Get<string>("summary");

            _realtime.LogInfo(
                message: "AI pipeline service execution completed.",
                category: "ai.pipeline.result",
                data: new
                {
                    context.ExecutionId,
                    Result = result
                });

            return result ?? string.Empty;
        }
    }
}