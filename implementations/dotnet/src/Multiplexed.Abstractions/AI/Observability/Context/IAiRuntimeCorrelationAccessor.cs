namespace Multiplexed.Abstractions.AI.Observability.Context
{
    /// <summary>
    /// Provides access to the ambient runtime correlation context for the current asynchronous flow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This accessor allows metrics, tracing, logging, ledger helpers, controller flows,
    /// and worker flows to read the current runtime correlation information without passing
    /// correlation parameters through every method signature.
    /// </para>
    ///
    /// <para>
    /// The accessor is passive. It must not load execution records, access the DAG store,
    /// create execution state, or mutate durable runtime state.
    /// </para>
    ///
    /// <para>
    /// Implementations may use <see cref="System.Threading.AsyncLocal{T}"/> to flow the
    /// correlation context within the current process and asynchronous call chain.
    /// This does not provide cross-process propagation.
    /// </para>
    ///
    /// <para>
    /// Distributed propagation must still be done explicitly through queued run metadata,
    /// execution metadata, durable execution records, or messages.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeCorrelationAccessor
    {
        /// <summary>
        /// Gets the current runtime execution correlation context for the active asynchronous flow.
        /// </summary>
        AiRuntimeExecutionCorrelationContext? Current { get; }

        /// <summary>
        /// Pushes a runtime execution correlation context for the current asynchronous flow.
        /// </summary>
        /// <param name="context">The correlation context to make current.</param>
        /// <returns>
        /// A disposable scope that restores the previous correlation context when disposed.
        /// </returns>
        IDisposable Push(AiRuntimeExecutionCorrelationContext context);
    }
}