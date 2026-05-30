namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Provides a timeline view of AI runtime trace events.
    /// </summary>
    public interface IAiTraceTimeline
    {
        /// <summary>
        /// Adds a new trace event to the timeline.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        void Add(AiTraceEvent traceEvent);

        /// <summary>
        /// Gets all trace events for a given execution ordered by time.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <returns>The ordered list of trace events.</returns>
        IReadOnlyList<AiTraceEvent> Get(string executionId);

        /// <summary>
        /// Clears all trace events for a given execution.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        void Clear(string executionId);
    }
}