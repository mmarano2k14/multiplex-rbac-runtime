namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Replay
{
    /// <summary>
    /// Represents a stable comparable replay fingerprint.
    /// </summary>
    public sealed class EnterpriseRuntimeReplayFingerprint
    {
        /// <summary>
        /// Gets or initializes the execution status.
        /// </summary>
        public required string Status { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether the execution is terminal.
        /// </summary>
        public required bool IsTerminal { get; init; }

        /// <summary>
        /// Gets or initializes the completed step names.
        /// </summary>
        public required IReadOnlyList<string> CompletedSteps { get; init; }

        /// <summary>
        /// Gets or initializes the selected step statuses.
        /// </summary>
        public required IReadOnlyDictionary<string, string> StepStatuses { get; init; }

        /// <summary>
        /// Gets or initializes the retry counts by step name.
        /// </summary>
        public required IReadOnlyDictionary<string, int> RetryCounts { get; init; }

        /// <summary>
        /// Gets or initializes the required resolved step statuses.
        /// </summary>
        public required IReadOnlyDictionary<string, string> RequiredResolvedSteps { get; init; }
    }
}