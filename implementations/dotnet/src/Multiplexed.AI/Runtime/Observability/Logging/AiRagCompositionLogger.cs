using System;
using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Observability.Logging
{
    /// <summary>
    /// Emits structured realtime events for RAG composition execution.
    ///
    /// PURPOSE:
    /// - Provides composition-level observability inside the AI runtime.
    /// - Makes context-building activity visible for debugging, monitoring,
    ///   and replay analysis.
    ///
    /// DESIGN:
    /// - This logger is purely observational.
    /// - It delegates all event emission to <see cref="IRuntimeEventContext"/>.
    /// - It must remain lightweight and safe to use in hot execution paths.
    /// </summary>
    public sealed class AiRagCompositionLogger : IAiRagCompositionLogger
    {
        private readonly IRuntimeEventContext _realtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiRagCompositionLogger"/> class.
        /// </summary>
        /// <param name="realtime">
        /// The runtime event sink used for observability.
        /// </param>
        public AiRagCompositionLogger(IRuntimeEventContext realtime)
        {
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }

        /// <inheritdoc />
        public void CompositionStarted(
            string? executionId,
            string composerName,
            int itemCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(composerName);

            _realtime.LogInfo(
                message: $"RAG composition '{composerName}' started.",
                category: "ai.rag.composition.start",
                data: new
                {
                    ExecutionId = executionId,
                    Composer = composerName,
                    ItemCount = itemCount
                });
        }

        /// <inheritdoc />
        public void CompositionCompleted(
            string? executionId,
            string composerName,
            int fragmentCount,
            long durationMs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(composerName);

            _realtime.LogInfo(
                message: $"RAG composition '{composerName}' completed.",
                category: "ai.rag.composition.completed",
                data: new
                {
                    ExecutionId = executionId,
                    Composer = composerName,
                    FragmentCount = fragmentCount,
                    DurationMs = durationMs
                });
        }

        /// <inheritdoc />
        public void CompositionFailed(
            string? executionId,
            string composerName,
            long durationMs,
            Exception exception)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(composerName);
            ArgumentNullException.ThrowIfNull(exception);

            _realtime.LogError(
                message: $"RAG composition '{composerName}' failed.",
                category: "ai.rag.composition.failed",
                data: new
                {
                    ExecutionId = executionId,
                    Composer = composerName,
                    DurationMs = durationMs,
                    Exception = exception.Message
                });
        }
    }
}