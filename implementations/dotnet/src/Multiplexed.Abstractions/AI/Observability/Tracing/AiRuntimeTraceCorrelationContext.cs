using Multiplexed.Abstractions.AI.Observability.Context;

namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Carries correlation data attached to runtime trace records and projected
    /// trace events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This context wraps <see cref="AiRuntimeExecutionCorrelationContext"/> for
    /// shared run, execution, pipeline, runtime instance, and worker correlation.
    /// </para>
    ///
    /// <para>
    /// Trace-specific dimensions such as step identity, claim token, provider,
    /// model, operation, payload references, and trace scope identifiers are kept
    /// on this object because they are not part of the ambient execution correlation
    /// context.
    /// </para>
    ///
    /// <para>
    /// This object is observational only. It must not influence runtime execution,
    /// retry behavior, retention decisions, concurrency leases, or DAG state mutation.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeTraceCorrelationContext
    {
        /// <summary>
        /// Gets or sets the shared runtime execution correlation context.
        /// </summary>
        public AiRuntimeExecutionCorrelationContext? Runtime { get; set; }

        /// <summary>
        /// Gets or sets the logical DAG step identifier associated with the trace.
        /// </summary>
        public string? StepId { get; set; }

        /// <summary>
        /// Gets or sets the technical runtime step key associated with the trace.
        /// </summary>
        public string? StepKey { get; set; }

        /// <summary>
        /// Gets or sets the distributed claim token associated with the trace.
        /// </summary>
        public string? ClaimToken { get; set; }

        /// <summary>
        /// Gets or sets the policy key associated with a policy evaluation.
        /// </summary>
        public string? PolicyKey { get; set; }

        /// <summary>
        /// Gets or sets the provider associated with the current operation.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the model associated with the current operation.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the logical operation associated with the current trace.
        /// </summary>
        public string? Operation { get; set; }

        /// <summary>
        /// Gets or sets the optional input payload reference associated with the trace.
        /// </summary>
        public string? InputPayloadRef { get; set; }

        /// <summary>
        /// Gets or sets the optional output payload reference associated with the trace.
        /// </summary>
        public string? OutputPayloadRef { get; set; }

        /// <summary>
        /// Gets or sets the optional human input reference associated with the trace.
        /// </summary>
        public string? HumanInputRef { get; set; }

        /// <summary>
        /// Gets or sets the optional prompt reference associated with the trace.
        /// </summary>
        public string? PromptRef { get; set; }

        /// <summary>
        /// Gets or sets the distributed trace identifier.
        /// </summary>
        public string? TraceId { get; set; }

        /// <summary>
        /// Gets or sets the trace scope identifier associated with this trace event.
        /// </summary>
        public string? TraceScopeId { get; set; }

        /// <summary>
        /// Gets or sets the parent trace scope identifier when this trace is nested.
        /// </summary>
        public string? ParentTraceScopeId { get; set; }

        /// <summary>
        /// Gets or sets the trace source that produced this event.
        /// </summary>
        public string? Source { get; set; }
    }
}