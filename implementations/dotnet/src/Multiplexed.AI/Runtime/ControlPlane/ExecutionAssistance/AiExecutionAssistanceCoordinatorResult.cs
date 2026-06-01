using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;

namespace Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Represents the result of one execution assistance coordinator evaluation cycle.
    /// </summary>
    public sealed class AiExecutionAssistanceCoordinatorResult
    {
        /// <summary>
        /// Gets a value indicating whether execution assistance was enabled.
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// Gets the helper runtime instance identifier that performed the evaluation.
        /// </summary>
        public string? HelperRuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the number of active candidates inspected by the coordinator.
        /// </summary>
        public int CandidateCount { get; init; }

        /// <summary>
        /// Gets the number of candidates skipped by the coordinator.
        /// </summary>
        public int SkippedCandidateCount { get; init; }

        /// <summary>
        /// Gets the number of assistance decisions evaluated by the coordinator.
        /// </summary>
        public int EvaluatedDecisionCount { get; init; }

        /// <summary>
        /// Gets the number of assistance leases granted by the coordinator.
        /// </summary>
        public int GrantedLeaseCount { get; init; }

        /// <summary>
        /// Gets the number of assistance pump operations started by the coordinator.
        /// </summary>
        public int StartedPumpCount { get; init; }

        /// <summary>
        /// Gets assistance decisions evaluated during this cycle.
        /// </summary>
        public IReadOnlyCollection<AiExecutionAssistanceDecision> Decisions { get; init; } =
            Array.Empty<AiExecutionAssistanceDecision>();

        /// <summary>
        /// Gets assistance pump results produced during this cycle.
        /// </summary>
        public IReadOnlyCollection<AiExecutionAssistancePumpResult> PumpResults { get; init; } =
            Array.Empty<AiExecutionAssistancePumpResult>();

        /// <summary>
        /// Gets additional metadata associated with the coordinator result.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}