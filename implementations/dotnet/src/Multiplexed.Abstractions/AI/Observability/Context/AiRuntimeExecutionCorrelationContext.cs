namespace Multiplexed.Abstractions.AI.Observability.Context
{
    /// <summary>
    /// Carries ambient runtime correlation information for the current asynchronous execution flow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This context is used to correlate controller runs, queued executions, runtime workers,
    /// metrics, tracing, ledger enrichment, and future replay diagnostics.
    /// </para>
    ///
    /// <para>
    /// Unlike <see cref="AiRuntimeLedgerEventCorrelationContext"/>, this context can exist before a durable
    /// DAG execution identifier is known. This makes it suitable for the controller and enqueue
    /// lifecycle where a <c>RunId</c> and <c>CorrelationId</c> exist before an <c>ExecutionId</c>.
    /// </para>
    ///
    /// <para>
    /// This object must be scoped to a single queued run or execution flow. It must never be reused
    /// across unrelated runs.
    /// </para>
    ///
    /// <para>
    /// This context is not distributed state. When used with an AsyncLocal-based accessor, it only
    /// flows within the current process and asynchronous call chain. Cross-process propagation must
    /// still be done explicitly through queued run metadata, execution metadata, or durable state.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeExecutionCorrelationContext
    {
        /// <summary>
        /// Gets the stable correlation identifier used to connect logs, metrics, traces,
        /// ledger entries, controller runs, and external diagnostics.
        /// </summary>
        public required string CorrelationId { get; init; }

        /// <summary>
        /// Gets the optional controller or background queue run identifier.
        /// </summary>
        public string? RunId { get; set; }

        /// <summary>
        /// Gets or sets the durable DAG execution identifier once it becomes known.
        /// </summary>
        public string? ExecutionId { get; set; }

        /// <summary>
        /// Gets the pipeline name associated with the current run or execution.
        /// </summary>
        public string? PipelineName { get; init; }

        /// <summary>
        /// Gets the pipeline version associated with the current run or execution.
        /// </summary>
        public string? PipelineVersion { get; init; }

        /// <summary>
        /// Gets or sets the stable pipeline key associated with the current run or execution.
        /// </summary>
        public string? PipelineKey { get; set; }

        /// <summary>
        /// Gets or sets the runtime instance identifier for the current process, host, or pod.
        /// </summary>
        public string? RuntimeInstanceId { get; set; }

        /// <summary>
        /// Gets or sets the worker identifier for the current runtime worker loop.
        /// </summary>
        public string? WorkerId { get; set; }
    }
}