namespace Multiplexed.Abstractions.AI.Observability.Context
{
    /// <summary>
    /// Carries stable correlation identifiers used to connect runtime execution,
    /// controller runs, DAG steps, distributed workers, tracing, metrics, ledger entries,
    /// and future replay reports.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ExecutionId"/> is the primary correlation key.
    /// </para>
    /// <para>
    /// <see cref="RunId"/> represents the controller or queue lifecycle.
    /// </para>
    /// <para>
    /// <see cref="StepId"/> and <see cref="StepKey"/> represent the DAG step lifecycle.
    /// </para>
    /// <para>
    /// <see cref="RuntimeInstanceId"/> and <see cref="WorkerId"/> represent distributed ownership.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeCorrelationContext
    {
        /// <summary>
        /// Gets the primary execution identifier used to correlate state, ledger,
        /// tracing, metrics, and replay information.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the optional controller run identifier associated with the execution.
        /// </summary>
        public string? RunId { get; init; }

        /// <summary>
        /// Gets the pipeline name associated with the execution.
        /// </summary>
        public string? PipelineName { get; init; }

        /// <summary>
        /// Gets the pipeline version associated with the execution.
        /// </summary>
        public string? PipelineVersion { get; init; }

        /// <summary>
        /// Gets the DAG step identifier associated with the current runtime operation.
        /// </summary>
        public string? StepId { get; init; }

        /// <summary>
        /// Gets the logical step key associated with the current runtime operation.
        /// </summary>
        public string? StepKey { get; init; }

        /// <summary>
        /// Gets the runtime instance identifier that produced the runtime decision.
        /// </summary>
        public string? RuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the worker identifier that produced the runtime decision.
        /// </summary>
        public string? WorkerId { get; init; }

        /// <summary>
        /// Gets the claim token associated with distributed step ownership.
        /// </summary>
        public string? ClaimToken { get; init; }

        /// <summary>
        /// Gets the policy key associated with a policy evaluation.
        /// </summary>
        public string? PolicyKey { get; init; }

        /// <summary>
        /// Gets the provider associated with the current operation.
        /// </summary>
        public string? Provider { get; init; }

        /// <summary>
        /// Gets the model associated with the current operation.
        /// </summary>
        public string? Model { get; init; }

        /// <summary>
        /// Gets the logical operation associated with the current runtime decision.
        /// </summary>
        public string? Operation { get; init; }

        /// <summary>
        /// Gets the optional input payload reference associated with the runtime operation.
        /// </summary>
        public string? InputPayloadRef { get; init; }

        /// <summary>
        /// Gets the optional output payload reference associated with the runtime operation.
        /// </summary>
        public string? OutputPayloadRef { get; init; }

        /// <summary>
        /// Gets the optional human input reference associated with the runtime operation.
        /// </summary>
        public string? HumanInputRef { get; init; }

        /// <summary>
        /// Gets the optional prompt reference associated with the runtime operation.
        /// </summary>
        public string? PromptRef { get; init; }

        /// <summary>
        /// Gets the distributed tracing identifier associated with the runtime operation.
        /// </summary>
        public string? TraceId { get; init; }

        /// <summary>
        /// Gets the general correlation identifier used to connect logs, traces,
        /// metrics, ledger entries, and external systems.
        /// </summary>
        public string? CorrelationId { get; init; }
    }
}