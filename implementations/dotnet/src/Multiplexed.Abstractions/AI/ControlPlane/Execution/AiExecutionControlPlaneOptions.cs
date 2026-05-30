namespace Multiplexed.Abstractions.AI.ControlPlane.Execution
{
    /// <summary>
    /// Defines options for the execution control-plane facade.
    ///
    /// These options only control the high-level control-plane layer.
    /// They do not modify the execution engine, local queues, workers, or DAG state.
    /// </summary>
    public sealed class AiExecutionControlPlaneOptions
    {
        /// <summary>
        /// Enables execution pause operations.
        /// </summary>
        public bool EnablePause { get; init; } = true;

        /// <summary>
        /// Enables execution resume operations.
        /// </summary>
        public bool EnableResume { get; init; } = true;

        /// <summary>
        /// Enables execution cancel operations.
        /// </summary>
        public bool EnableCancel { get; init; } = true;

        /// <summary>
        /// Enables human or external input submission operations.
        /// </summary>
        public bool EnableSubmitHumanInput { get; init; } = true;

        /// <summary>
        /// Enables durable execution control state retrieval.
        /// </summary>
        public bool EnableGetStatus { get; init; } = true;

        /// <summary>
        /// When enabled, expected operational failures should be returned as
        /// structured failed results instead of being thrown to the caller.
        /// </summary>
        public bool ReturnFailureResultInsteadOfThrowing { get; init; } = true;

        /// <summary>
        /// Enables controller-level duration measurement.
        ///
        /// This is useful for future Grafana metrics and control-plane diagnostics.
        /// </summary>
        public bool MeasureDuration { get; init; } = true;
    }
}