namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    /// <summary>
    /// Represents an API-level replay request.
    /// </summary>
    public sealed class AiExecutionReplayApiRequest
    {
        /// <summary>
        /// Gets the replay mode requested by the caller.
        /// </summary>
        public AiExecutionReplayMode Mode { get; init; } =
            AiExecutionReplayMode.AuditOnly;

        /// <summary>
        /// Gets whether the replay response should include per-step details.
        /// </summary>
        public bool IncludeStepDetails { get; init; }

        /// <summary>
        /// Gets whether the replay response should include execution-correlated ledger events.
        /// </summary>
        public bool IncludeLedgerEvents { get; init; }

        /// <summary>
        /// Gets whether payload references should be validated.
        /// </summary>
        public bool ValidatePayloadReferences { get; init; } = true;

        /// <summary>
        /// Converts this API request into a runtime replay request.
        /// </summary>
        public AiExecutionReplayRequest ToReplayRequest(
            string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException(
                    "ExecutionId cannot be null or empty.",
                    nameof(executionId));
            }

            return new AiExecutionReplayRequest
            {
                ExecutionId = executionId,
                Mode = Mode,
                IncludeStepDetails = IncludeStepDetails,
                IncludeLedgerEvents = IncludeLedgerEvents,
                ValidatePayloadReferences = ValidatePayloadReferences
            };
        }
    }
}