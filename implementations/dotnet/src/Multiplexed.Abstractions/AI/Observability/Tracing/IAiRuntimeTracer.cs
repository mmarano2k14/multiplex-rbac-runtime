namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Central tracing facade for the AI runtime.
    /// </summary>
    /// <remarks>
    /// This abstraction intentionally hides any concrete tracing technology from the runtime.
    /// The engine should depend on this interface instead of depending directly on
    /// OpenTelemetry, ActivitySource, exporters, or vendor-specific SDKs.
    /// </remarks>
    public interface IAiRuntimeTracer
    {
        /// <summary>
        /// Starts a trace scope for a full AI execution.
        /// </summary>
        /// <param name="context">The execution tracing context.</param>
        /// <returns>A trace scope representing the execution lifetime.</returns>
        IAiTraceScope StartExecution(AiExecutionTraceContext context);

        /// <summary>
        /// Starts a trace scope for a single AI step execution.
        /// </summary>
        /// <param name="context">The step tracing context.</param>
        /// <returns>A trace scope representing the step lifetime.</returns>
        IAiTraceScope StartStep(AiStepTraceContext context);

        /// <summary>
        /// Starts a trace scope for a retention operation.
        /// </summary>
        /// <param name="context">The retention tracing context.</param>
        /// <returns>A trace scope representing the retention operation lifetime.</returns>
        IAiTraceScope StartRetention(AiRetentionTraceContext context);

        /// <summary>
        /// Starts a trace scope for a storage operation.
        /// </summary>
        /// <param name="context">The storage tracing context.</param>
        /// <returns>A trace scope representing the storage operation lifetime.</returns>
        IAiTraceScope StartStorage(AiStorageTraceContext context);

        /// <summary>
        /// Starts a trace scope for a resolver operation.
        /// </summary>
        /// <param name="context">The resolver tracing context.</param>
        /// <returns>A trace scope representing the resolver operation lifetime.</returns>
        IAiTraceScope StartResolver(AiResolverTraceContext context);
    }
}