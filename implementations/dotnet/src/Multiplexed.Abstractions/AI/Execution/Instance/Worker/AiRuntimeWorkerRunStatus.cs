namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Represents the controller-level lifecycle status of a runtime worker run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This status tracks the lifecycle of a pipeline run submitted to a runtime
    /// background controller.
    /// </para>
    /// <para>
    /// It is separate from <c>AiExecutionStatus</c>, which represents the persisted
    /// runtime execution record status after an execution has been created.
    /// </para>
    /// </remarks>
    public enum AiRuntimeWorkerRunStatus
    {
        /// <summary>
        /// The run has been accepted by the controller and is waiting in the queue.
        /// </summary>
        Queued = 0,

        /// <summary>
        /// The controller is creating the runtime execution record and DAG state.
        /// </summary>
        CreatingExecution = 1,

        /// <summary>
        /// The runtime execution has been created and is being advanced by a worker.
        /// </summary>
        Running = 2,

        /// <summary>
        /// The run is paused.
        /// </summary>
        Paused = 3,

        /// <summary>
        /// The run completed successfully.
        /// </summary>
        Completed = 4,

        /// <summary>
        /// The run failed.
        /// </summary>
        Failed = 5,

        /// <summary>
        /// The run was cancelled.
        /// </summary>
        Cancelled = 6
    }
}