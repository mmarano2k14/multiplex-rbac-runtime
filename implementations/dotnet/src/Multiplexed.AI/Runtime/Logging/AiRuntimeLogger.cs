namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimeLogger"/>.
    ///
    /// This class acts as the root runtime logging gateway and exposes
    /// specialized loggers for each execution area of the AI runtime.
    /// </summary>
    public sealed class AiRuntimeLogger : IAiRuntimeLogger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeLogger"/> class.
        /// </summary>
        /// <param name="engine">The logger for persisted execution engine events.</param>
        /// <param name="pipeline">The logger for sequential pipeline runner events.</param>
        /// <param name="pipelineService">The logger for high-level pipeline service events.</param>
        /// <param name="stepExecutor">The logger for single-step executor events.</param>
        public AiRuntimeLogger(
            IAiExecutionEngineLogger engine,
            IAiPipelineLogger pipeline,
            IAiPipelineServiceLogger pipelineService,
            IAiStepExecutorLogger stepExecutor)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            Pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            PipelineService = pipelineService ?? throw new ArgumentNullException(nameof(pipelineService));
            StepExecutor = stepExecutor ?? throw new ArgumentNullException(nameof(stepExecutor));
        }

        /// <inheritdoc />
        public IAiExecutionEngineLogger Engine { get; }

        /// <inheritdoc />
        public IAiPipelineLogger Pipeline { get; }

        /// <inheritdoc />
        public IAiPipelineServiceLogger PipelineService { get; }

        /// <inheritdoc />
        public IAiStepExecutorLogger StepExecutor { get; }
    }
}