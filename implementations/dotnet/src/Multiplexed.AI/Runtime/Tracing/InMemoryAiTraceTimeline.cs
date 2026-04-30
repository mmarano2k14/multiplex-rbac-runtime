using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.Abstractions.AI.Tracing;

namespace Multiplexed.AI.Runtime.Tracing
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
        public void Add(AiTraceEvent traceEvent)
        {
            ArgumentNullException.ThrowIfNull(traceEvent);

            if (string.IsNullOrWhiteSpace(traceEvent.ExecutionId))
            {
                throw new ArgumentException(
                    "Trace event execution id cannot be null or whitespace.",
                    nameof(traceEvent));
            }

            if (traceEvent.TimestampUtc == default)
            {
                traceEvent.TimestampUtc = DateTime.UtcNow;
            }

            var events = _eventsByExecutionId.GetOrAdd(
                traceEvent.ExecutionId,
                _ => new List<AiTraceEvent>());

            lock (events)
            {
                events.Add(traceEvent);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<AiTraceEvent> Get(string executionId)
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
        public void Clear(string executionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            _eventsByExecutionId.TryRemove(executionId, out _);
        }
    }
}