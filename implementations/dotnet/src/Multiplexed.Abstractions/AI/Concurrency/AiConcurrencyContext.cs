namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Represents the runtime context used to evaluate and acquire distributed concurrency capacity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This context identifies the execution, pipeline, step, runtime instance, and lease involved
    /// in distributed admission control.
    /// </para>
    ///
    /// <para>
    /// The context is used by the concurrency gate to build distributed throttling scopes such as:
    /// global, pipeline, pipeline-step, execution, runtime instance, provider, model, and operation.
    /// </para>
    ///
    /// <para>
    /// The concurrency context does not decide whether execution is allowed. It only carries the
    /// identifiers required by the concurrency engine or gate to evaluate and acquire capacity.
    /// </para>
    /// </remarks>
    public sealed class AiConcurrencyContext
    {
        /// <summary>
        /// Gets or sets the concrete execution identifier.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value identifies one specific runtime execution instance.
        /// </para>
        ///
        /// <para>
        /// It is used for execution-level throttling, for example limiting how many DAG steps from
        /// the same execution may run concurrently.
        /// </para>
        /// </remarks>
        public required string ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the stable logical pipeline key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This key must be stable across multiple executions of the same pipeline.
        /// </para>
        ///
        /// <para>
        /// A recommended format is <c>{PipelineName}:{PipelineVersion}</c>.
        /// </para>
        ///
        /// <para>
        /// This is used for pipeline-level throttling. Multiple executions of the same pipeline
        /// share the same pipeline scope.
        /// </para>
        /// </remarks>
        public required string PipelineKey { get; set; }

        /// <summary>
        /// Gets or sets the concrete step identifier.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the current DAG implementation this is usually the step name.
        /// </para>
        ///
        /// <para>
        /// This value identifies the concrete step candidate involved in admission control.
        /// The actual step ownership guarantee is still enforced by the distributed DAG claim.
        /// </para>
        /// </remarks>
        public required string StepId { get; set; }

        /// <summary>
        /// Gets or sets the logical step key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value identifies the logical step type or stable step name used for step-level
        /// throttling.
        /// </para>
        ///
        /// <para>
        /// The Redis gate combines this value with <see cref="PipelineKey"/> to create the
        /// <c>pipeline-step</c> scope. This avoids unrelated pipelines throttling each other when
        /// they use the same step name.
        /// </para>
        /// </remarks>
        public required string StepKey { get; set; }

        /// <summary>
        /// Gets or sets the runtime instance or worker identifier.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is used for instance-level throttling.
        /// </para>
        ///
        /// <para>
        /// It prevents one runtime instance from consuming all available distributed capacity.
        /// </para>
        /// </remarks>
        public required string RuntimeInstanceId { get; set; }

        /// <summary>
        /// Gets or sets the distributed concurrency lease identifier.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The lease id is stored as the Redis sorted-set member for every acquired concurrency scope.
        /// </para>
        ///
        /// <para>
        /// The same lease id must be used during acquisition and release.
        /// </para>
        ///
        /// <para>
        /// The current recommended format is <c>{ExecutionId}:{StepName}:{WorkerId}</c>.
        /// </para>
        /// </remarks>
        public required string LeaseId { get; set; }

        /// <summary>
        /// Gets or sets the external provider associated with the step, when applicable.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is optional and is only used when provider-level throttling is configured.
        /// </para>
        ///
        /// <para>
        /// Examples include <c>openai</c>, <c>anthropic</c>, <c>bedrock</c>, <c>redis-vector</c>,
        /// <c>postgres-vector</c>, or another external system used by the step.
        /// </para>
        ///
        /// <para>
        /// If this value is empty, provider-level throttling is skipped even when other concurrency
        /// scopes are active.
        /// </para>
        /// </remarks>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the provider model associated with the step, when applicable.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is optional and is only used when model-level throttling is configured.
        /// </para>
        ///
        /// <para>
        /// Examples include <c>gpt-4.1</c>, <c>gpt-4o</c>, <c>claude-sonnet</c>, or an embedding model.
        /// </para>
        ///
        /// <para>
        /// Model-level throttling should normally be combined with <see cref="Provider"/> so the
        /// scope remains unambiguous.
        /// </para>
        /// </remarks>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the logical operation associated with the step, when applicable.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is optional and is only used when operation-level throttling is configured.
        /// </para>
        ///
        /// <para>
        /// Examples include <c>llm.chat</c>, <c>embedding.create</c>, <c>rag.retrieve</c>,
        /// <c>vector.search</c>, <c>rerank</c>, or <c>tool.call</c>.
        /// </para>
        ///
        /// <para>
        /// Operation-level throttling is useful when multiple providers or steps perform the same
        /// logical operation and should share a distributed capacity limit.
        /// </para>
        /// </remarks>
        public string? Operation { get; set; }
    }
}