namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Realtime.Formatting
{
    /// <summary>
    /// Defines readable enterprise runtime realtime event kinds.
    /// </summary>
    public enum EnterpriseRuntimeRealtimeEventKind
    {
        /// <summary>
        /// Unknown or unsupported realtime event.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A step was claimed by a worker.
        /// </summary>
        StepClaimed = 1,

        /// <summary>
        /// A step completed successfully.
        /// </summary>
        StepCompleted = 2,

        /// <summary>
        /// A step failed.
        /// </summary>
        StepFailed = 3,

        /// <summary>
        /// A retry or recovery-related event occurred.
        /// </summary>
        RetryOrRecovery = 4,

        /// <summary>
        /// Execution finalization was attempted.
        /// </summary>
        FinalizationAttempt = 5,

        /// <summary>
        /// Execution finalization succeeded.
        /// </summary>
        FinalizationSucceeded = 6,

        /// <summary>
        /// Execution finalization lost a distributed race.
        /// </summary>
        FinalizationRaceLost = 7,

        /// <summary>
        /// A terminal execution snapshot was persisted.
        /// </summary>
        SnapshotPersisted = 8,

        /// <summary>
        /// Execution cleanup was skipped.
        /// </summary>
        CleanupSkipped = 9,

        /// <summary>
        /// Execution replay restored state from a snapshot.
        /// </summary>
        ReplayRestored = 10,

        /// <summary>
        /// A worker idle-delay event occurred.
        /// </summary>
        WorkerIdle = 11,

        /// <summary>
        /// A diagnostic runtime event occurred.
        /// </summary>
        Diagnostic = 12
    }
}