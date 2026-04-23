namespace Multiplexed.Abstractions.AI.Execution.Cleanup
{
    /// <summary>
    /// Represents the outcome of a coordinated AI execution bundle cleanup.
    /// </summary>
    public sealed class AiExecutionCleanupResult
    {
        public string ExecutionId { get; init; } = string.Empty;

        public bool RecordFound { get; init; }
        public bool RecordDeleted { get; init; }

        public bool StateFound { get; init; }
        public bool StateDeleted { get; init; }

        public int StepCountDiscovered { get; init; }
        public int StepCountDeleted { get; init; }

        public bool ClaimsFound { get; init; }
        public bool ClaimsDeleted { get; init; }

        public bool RbacContextFound { get; init; }
        public bool RbacContextDeleted { get; init; }

        public bool InFlightFound { get; init; }
        public bool InFlightDeleted { get; init; }

        /// <summary>
        /// True when no blocking cleanup error remains.
        /// This does not mean every resource existed.
        /// It means the cleanup converged successfully.
        /// </summary>
        public bool IsComplete { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }
}