namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Defines the main entry point for AI runtime logging.
    ///
    /// This abstraction groups the specialized runtime loggers used by:
    /// - the persisted execution engine
    /// - the sequential pipeline runner
    /// - the high-level pipeline service
    /// - the step executor
    ///
    /// The goal is to provide a single dependency to inject while preserving
    /// clear separation of logging responsibilities internally.
    /// </summary>
    public interface IAiRuntimeLogger
    {
        /// <summary>
        /// Gets the logger dedicated to persisted execution engine events.
        /// </summary>
        IAiExecutionEngineLogger Engine { get; }

        /// <summary>
        /// Gets the logger dedicated to sequential pipeline runner events.
        /// </summary>
        IAiPipelineLogger Pipeline { get; }

        /// <summary>
        /// Gets the logger dedicated to high-level pipeline service events.
        /// </summary>
        IAiPipelineServiceLogger PipelineService { get; }

        /// <summary>
        /// Gets the logger dedicated to single-step executor events.
        /// </summary>
        IAiStepExecutorLogger StepExecutor { get; }

        /// <summary>
        /// Gets the logger dedicated to RAG runtime events.
        /// </summary>
        IAiRagLogger Rag { get; }
    }
}