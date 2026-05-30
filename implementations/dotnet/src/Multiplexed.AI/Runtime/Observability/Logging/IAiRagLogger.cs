namespace Multiplexed.AI.Runtime.Observability.Logging
{
    /// <summary>
    /// Defines the root logging gateway for RAG runtime events.
    ///
    /// PURPOSE:
    /// - Groups all RAG-specific runtime loggers behind a single abstraction.
    /// - Provides a consistent injection point for retrieval and composition logging.
    ///
    /// DESIGN:
    /// - Retrieval and composition remain separated to preserve responsibility clarity.
    /// - This interface acts as a small root logger for the RAG subsystem.
    /// </summary>
    public interface IAiRagLogger
    {
        /// <summary>
        /// Gets the logger dedicated to retrieval orchestration events.
        /// </summary>
        IAiRagRetrievalLogger Retrieval { get; }

        /// <summary>
        /// Gets the logger dedicated to composition execution events.
        /// </summary>
        IAiRagCompositionLogger Composition { get; }
    }
}