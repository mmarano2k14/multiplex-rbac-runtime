using System;
using Multiplexed.Abstractions.AI.Observability.Tracing;

namespace Multiplexed.AI.Runtime.Observability.Tracing
{
    /// <summary>
    /// Default trace scope implementation used by runtime tracers.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Represents the lifetime of a traced operation.
    /// - Captures success, failure, tags, completion time, and duration.
    /// - Records the trace through <see cref="IAiTraceRecorder"/> when disposed.
    ///
    /// IMPORTANT:
    /// - The scope records only once.
    /// - Disposal is idempotent.
    /// - The scope does not throw during tracing operations.
    /// </remarks>
    public sealed class AiTraceScope : IAiTraceScope
    {
        private readonly IAiTraceRecorder _recorder;
        private readonly AiTraceRecord _record;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiTraceScope"/> class.
        /// </summary>
        /// <param name="recorder">The trace recorder.</param>
        /// <param name="record">The trace record owned by this scope.</param>
        public AiTraceScope(
            IAiTraceRecorder recorder,
            AiTraceRecord record)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _record = record ?? throw new ArgumentNullException(nameof(record));
        }

        /// <inheritdoc />
        public void SetSuccess()
        {
            if (_disposed)
            {
                return;
            }

            _record.Succeeded = true;
            _record.Failed = false;
        }

        /// <inheritdoc />
        public void SetError(Exception exception)
        {
            if (_disposed)
            {
                return;
            }

            _record.Succeeded = false;
            _record.Failed = true;

            if (exception is not null)
            {
                _record.ErrorType = exception.GetType().FullName;
                _record.ErrorMessage = exception.Message;
            }
        }

        /// <inheritdoc />
        public void SetTag(string key, object? value)
        {
            if (_disposed || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _record.Tags[key] = value;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _record.CompletedAtUtc = DateTime.UtcNow;
            _recorder.Record(_record);
        }
    }
}