using System;

namespace Multiplexed.Abstractions.AI.Tracing
{
    /// <summary>
    /// Represents a runtime tracing scope.
    /// </summary>
    /// <remarks>
    /// A trace scope models the lifetime of one observable operation, such as an
    /// execution, step, resolver call, storage operation, retention action, or payload operation.
    /// 
    /// Implementations may forward scope information to OpenTelemetry, a custom runtime console,
    /// structured logs, or remain no-op depending on the configured tracing provider.
    /// </remarks>
    public interface IAiTraceScope : IDisposable
    {
        /// <summary>
        /// Marks the current trace scope as successfully completed.
        /// </summary>
        void SetSuccess();

        /// <summary>
        /// Marks the current trace scope as failed.
        /// </summary>
        /// <param name="exception">The exception associated with the failure.</param>
        void SetError(Exception exception);

        /// <summary>
        /// Adds or updates a tag on the current trace scope.
        /// </summary>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        void SetTag(string key, object? value);
    }
}