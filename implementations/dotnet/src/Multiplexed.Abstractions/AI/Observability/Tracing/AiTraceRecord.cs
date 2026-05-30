using System;
using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Represents a completed AI runtime trace record.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Captures the observable lifetime of one runtime operation.
    /// - Used by in-memory tracing, tests, diagnostics, and future exporters.
    ///
    /// EXAMPLES:
    /// - Execution trace
    /// - Step trace
    /// - Storage trace
    /// - Retention trace
    /// - Resolver trace
    /// </remarks>
    public sealed class AiTraceRecord
    {
        /// <summary>
        /// Gets the unique trace record identifier.
        /// </summary>
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the trace operation name.
        /// </summary>
        public string Operation { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the execution identifier.
        /// </summary>
        public string? ExecutionId { get; init; }

        /// <summary>
        /// Gets or sets the step identifier when the trace is step-scoped.
        /// </summary>
        public string? StepId { get; init; }

        /// <summary>
        /// Gets or sets the UTC timestamp at which the trace started.
        /// </summary>
        public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp at which the trace completed.
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets the trace duration when the trace has completed.
        /// </summary>
        public TimeSpan? Duration =>
            CompletedAtUtc.HasValue
                ? CompletedAtUtc.Value - StartedAtUtc
                : null;

        /// <summary>
        /// Gets or sets whether the traced operation completed successfully.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Gets or sets whether the traced operation failed.
        /// </summary>
        public bool Failed { get; set; }

        /// <summary>
        /// Gets or sets the exception type when the trace failed.
        /// </summary>
        public string? ErrorType { get; set; }

        /// <summary>
        /// Gets or sets the exception message when the trace failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets additional trace tags.
        /// </summary>
        public IDictionary<string, object?> Tags { get; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the runtime trace correlation snapshot attached to this trace record.
        /// </summary>
        public AiRuntimeTraceCorrelationContext? Correlation { get; set; }
    }
}