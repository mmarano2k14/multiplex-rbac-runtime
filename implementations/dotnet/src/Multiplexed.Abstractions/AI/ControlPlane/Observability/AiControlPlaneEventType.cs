namespace Multiplexed.Abstractions.AI.ControlPlane.Observability
{
    /// <summary>
    /// Defines generic control-plane observability event types.
    ///
    /// These event types are intentionally generic so they can be reused by
    /// replay, execution control, run control, instance registry, admission,
    /// shared queue, shared controller, and scaling operations.
    /// </summary>
    public enum AiControlPlaneEventType
    {
        /// <summary>
        /// A control-plane operation was requested.
        /// </summary>
        OperationRequested = 0,

        /// <summary>
        /// A control-plane operation started.
        /// </summary>
        OperationStarted = 1,

        /// <summary>
        /// A control-plane operation completed successfully.
        /// </summary>
        OperationCompleted = 2,

        /// <summary>
        /// A control-plane operation was denied.
        /// </summary>
        OperationDenied = 3,

        /// <summary>
        /// A control-plane operation failed.
        /// </summary>
        OperationFailed = 4,

        /// <summary>
        /// A control-plane operation produced diagnostics or validation issues.
        /// </summary>
        OperationDiagnostic = 5
    }
}