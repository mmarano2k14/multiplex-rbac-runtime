using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Execution.Instance.Worker
{
    /// <summary>
    /// Provides context for a background-controller pipeline run after terminal finalization.
    /// </summary>
    public sealed class AiRuntimePipelineRunFinalizedContext
    {
        /// <summary>
        /// Gets or sets the background controller run identifier.
        /// </summary>
        public required string RunId { get; init; }

        /// <summary>
        /// Gets or sets the runtime execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets or sets the finalized terminal execution record.
        /// </summary>
        public required AiExecutionRecord Record { get; init; }
    }
}