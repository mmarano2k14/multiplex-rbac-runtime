using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.Abstractions.AI.Observability.Tracing;

namespace Multiplexed.AI.Runtime.Observability.Tracing
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiTraceTimeline"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Stores trace timeline events per execution.
    /// - Provides an ordered timeline view for diagnostics, tests, console output, or a future UI.
    ///
    /// IMPORTANT:
    /// - This implementation is process-local and non-durable.
    /// - It is safe for concurrent writes per execution.
    /// - It should not be used as the final durable production observability backend.
    /// </remarks>
    public sealed class InMemoryAiTraceTimeline : IAiTraceTimeline
    {
        private readonly ConcurrentDictionary<string, List<AiTraceEvent>> _eventsByExecutionId =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public void Add(
            AiTraceEvent traceEvent)
        {
            ArgumentNullException.ThrowIfNull(traceEvent);

            var executionId = ResolveExecutionId(
                traceEvent);

            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException(
                    "Trace event execution id cannot be null or whitespace.",
                    nameof(traceEvent));
            }

            traceEvent.ExecutionId = executionId;

            if (traceEvent.TimestampUtc == default)
            {
                traceEvent.TimestampUtc = DateTime.UtcNow;
            }

            var events = _eventsByExecutionId.GetOrAdd(
                executionId,
                _ => new List<AiTraceEvent>());

            lock (events)
            {
                events.Add(traceEvent);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<AiTraceEvent> Get(
            string executionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            if (!_eventsByExecutionId.TryGetValue(executionId, out var events))
            {
                return Array.Empty<AiTraceEvent>();
            }

            lock (events)
            {
                return events
                    .OrderBy(x => x.TimestampUtc)
                    .ToList();
            }
        }

        /// <inheritdoc />
        public void Clear(
            string executionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _eventsByExecutionId.TryRemove(
                executionId,
                out _);
        }

        /// <summary>
        /// Resolves the execution identifier used as the timeline partition key.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        /// <returns>The resolved execution identifier.</returns>
        private static string? ResolveExecutionId(
            AiTraceEvent traceEvent)
        {
            return FirstNonEmpty(
                traceEvent.ExecutionId,
                traceEvent.Correlation?.Runtime?.ExecutionId,
                traceEvent.Correlation?.Runtime?.RunId,
                traceEvent.Correlation?.Runtime?.CorrelationId);
        }

        /// <summary>
        /// Returns the first non-empty value.
        /// </summary>
        /// <param name="values">The candidate values.</param>
        /// <returns>The first non-empty value, or <c>null</c>.</returns>
        private static string? FirstNonEmpty(
            params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }
    }
}