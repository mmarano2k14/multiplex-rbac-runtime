using Multiplexed.Abstractions.AI.Policies;

namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Defines distributed concurrency and throttling limits for an AI runtime step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This definition is the resolved concurrency configuration used by the runtime before a DAG
    /// step is claimed and executed.
    /// </para>
    ///
    /// <para>
    /// The current throttling implementation is config-driven. A dedicated throttling policy engine
    /// is not required at this stage, but <see cref="Policies"/> is preserved for compatibility with
    /// the existing resolver and future policy-driven concurrency behavior.
    /// </para>
    ///
    /// <para>
    /// The Redis concurrency gate uses this definition together with an
    /// <see cref="AiConcurrencyContext"/> to build distributed concurrency scopes and acquire
    /// crash-safe Redis leases.
    /// </para>
    /// </remarks>
    public sealed class AiConcurrencyDefinition
    {
        /// <summary>
        /// Gets or sets a value indicating whether distributed concurrency admission is enabled.
        /// </summary>
        /// <remarks>
        /// When disabled, execution is allowed without acquiring a distributed concurrency lease.
        /// </remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the concurrency policy keys associated with this definition.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These keys are preserved for compatibility with the existing concurrency resolver and
        /// future policy-driven throttling behavior.
        /// </para>
        ///
        /// <para>
        /// The current distributed throttling implementation remains config-driven.
        /// </para>
        /// </remarks>
        public List<AiConfiguredPolicyDefinition> Policies { get; set; } = new();

        /// <summary>
        /// Gets or sets the local maximum degree of parallelism used by the step execution orchestrator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a local execution bound and does not replace distributed concurrency admission.
        /// </para>
        ///
        /// <para>
        /// Distributed limits are enforced by Redis before step ownership is claimed. This value controls
        /// how many already-claimed steps can be executed concurrently inside the local process.
        /// </para>
        /// </remarks>
        public int? MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed across the entire runtime.
        /// </summary>
        /// <remarks>
        /// This limit applies globally across all pipelines, executions, steps, workers, and runtime instances.
        /// </remarks>
        public int? MaxGlobalConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed for the same logical pipeline.
        /// </summary>
        /// <remarks>
        /// The pipeline scope is based on the stable pipeline key, usually <c>{PipelineName}:{PipelineVersion}</c>.
        /// </remarks>
        public int? MaxPipelineConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed for the same step inside the same pipeline.
        /// </summary>
        /// <remarks>
        /// This is enforced as a <c>pipeline-step</c> scope using both pipeline key and step key.
        /// </remarks>
        public int? MaxStepConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed inside the same execution.
        /// </summary>
        /// <remarks>
        /// This supports bounded parallel DAG execution by limiting how many steps from the same execution
        /// can run at the same time.
        /// </remarks>
        public int? MaxExecutionConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed for the same runtime instance.
        /// </summary>
        /// <remarks>
        /// This prevents one worker or process from consuming all available distributed capacity.
        /// </remarks>
        public int? MaxInstanceConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed for the same external provider.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This limit is optional and is only applied when <see cref="AiConcurrencyContext.Provider"/>
        /// is available.
        /// </para>
        ///
        /// <para>
        /// Examples include <c>openai</c>, <c>anthropic</c>, <c>bedrock</c>, <c>redis-vector</c>,
        /// or another external provider used by the step.
        /// </para>
        /// </remarks>
        public int? MaxProviderConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed for the same provider model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This limit is optional and is only applied when both <see cref="AiConcurrencyContext.Provider"/>
        /// and <see cref="AiConcurrencyContext.Model"/> are available.
        /// </para>
        ///
        /// <para>
        /// Examples include <c>openai:gpt-4.1</c>, <c>openai:gpt-4o</c>,
        /// or <c>anthropic:claude-sonnet</c>.
        /// </para>
        /// </remarks>
        public int? MaxModelConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed for the same logical operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This limit is optional and is only applied when <see cref="AiConcurrencyContext.Operation"/>
        /// is available.
        /// </para>
        ///
        /// <para>
        /// Examples include <c>llm.chat</c>, <c>embedding.create</c>, <c>rag.retrieve</c>,
        /// <c>vector.search</c>, <c>rerank</c>, or <c>tool.call</c>.
        /// </para>
        /// </remarks>
        public int? MaxOperationConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the lease duration in seconds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The Redis gate stores each acquired lease with an expiration timestamp.
        /// </para>
        ///
        /// <para>
        /// If a worker crashes before releasing the lease, capacity is recovered after this duration
        /// when a later acquisition removes expired leases.
        /// </para>
        /// </remarks>
        public int LeaseSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default retry-after delay in milliseconds when concurrency admission is denied.
        /// </summary>
        /// <remarks>
        /// This value is returned in the denied concurrency decision and may be used by schedulers
        /// or callers to delay re-evaluation of throttled steps.
        /// </remarks>
        public int DefaultRetryAfterMs { get; set; } = 250;

        /// <summary>
        /// Gets or sets a value indicating whether retry-after jitter should be applied when admission is denied.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This does not affect Redis lease acquisition itself.
        /// </para>
        ///
        /// <para>
        /// It is intended for schedulers or callers that use the denied decision retry-after value
        /// to delay re-evaluation of throttled steps.
        /// </para>
        /// </remarks>
        public bool Jitter { get; set; }

        /// <summary>
        /// Gets or sets the maximum jitter delay in milliseconds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is used only when <see cref="Jitter"/> is enabled.
        /// </para>
        ///
        /// <para>
        /// The resolver may normalize this value to a safe default when not configured.
        /// </para>
        /// </remarks>
        public int MaxJitterMs { get; set; } = 100;

        /// <summary>
        /// Gets or sets generic distributed concurrency throttling rules resolved from policy configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These rules support generic policy configuration through <c>concurrency.throttle</c>.
        /// </para>
        ///
        /// <para>
        /// Rules are applied after the <see cref="AiConcurrencyContext"/> is created, because targeted
        /// rules must be matched against provider, model, operation, step, step-type, or pipeline values.
        /// </para>
        /// </remarks>
        public List<AiConcurrencyThrottleRule> ThrottleRules { get; set; } = new();
    }
}