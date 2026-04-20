using System;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Emits structured runtime events for RAG composition execution.
    ///
    /// PURPOSE:
    /// - Tracks composition lifecycle events.
    /// - Makes context-building activity visible in realtime.
    /// - Complements retrieval-level diagnostics with composition-level observability.
    ///
    /// DESIGN:
    /// - Composition logging is focused on orchestration visibility.
    /// - It does not perform composition itself.
    /// - It must remain lightweight and side-effect free.
    /// </summary>
    public interface IAiRagCompositionLogger
    {
        /// <summary>
        /// Emits an event when a composition operation starts.
        /// </summary>
        /// <param name="executionId">
        /// The optional runtime execution identifier.
        /// </param>
        /// <param name="composerName">
        /// The logical composer name.
        /// </param>
        /// <param name="itemCount">
        /// The number of input items provided to the composer.
        /// </param>
        void CompositionStarted(
            string? executionId,
            string composerName,
            int itemCount);

        /// <summary>
        /// Emits an event when a composition operation completes successfully.
        /// </summary>
        /// <param name="executionId">
        /// The optional runtime execution identifier.
        /// </param>
        /// <param name="composerName">
        /// The logical composer name.
        /// </param>
        /// <param name="fragmentCount">
        /// The number of fragments produced by composition.
        /// </param>
        /// <param name="durationMs">
        /// The execution duration in milliseconds.
        /// </param>
        void CompositionCompleted(
            string? executionId,
            string composerName,
            int fragmentCount,
            long durationMs);

        /// <summary>
        /// Emits an event when a composition operation fails.
        /// </summary>
        /// <param name="executionId">
        /// The optional runtime execution identifier.
        /// </param>
        /// <param name="composerName">
        /// The logical composer name.
        /// </param>
        /// <param name="durationMs">
        /// The execution duration in milliseconds.
        /// </param>
        /// <param name="exception">
        /// The exception thrown by composition.
        /// </param>
        void CompositionFailed(
            string? executionId,
            string composerName,
            long durationMs,
            Exception exception);
    }
}