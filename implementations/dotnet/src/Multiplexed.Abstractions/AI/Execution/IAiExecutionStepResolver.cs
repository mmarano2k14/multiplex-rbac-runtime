using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Resolves execution steps from state, cache, or external storage.
    ///
    /// PURPOSE:
    /// - Provide unified access to steps (in-memory + archived).
    /// - Support DAG convergence and dependency resolution.
    /// - Optimize performance with caching strategies.
    ///
    /// DESIGN:
    /// - Uses multi-layer resolution:
    ///   1. In-memory state
    ///   2. Local cache (index)
    ///   3. External store (Mongo/Redis)
    ///
    /// IMPORTANT:
    /// - MUST be safe for distributed usage.
    /// - MUST not cause excessive I/O.
    /// </summary>
    public interface IAiExecutionStepResolver
    {
        /// <summary>
        /// Warms the resolver cache for the given execution.
        ///
        /// PURPOSE:
        /// - Load archived step index in bulk.
        /// - Avoid repeated external calls.
        ///
        /// USE:
        /// - Called once at start of execution.
        /// </summary>
        Task WarmAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Warms only specific steps.
        ///
        /// PURPOSE:
        /// - Incrementally update cache after retention.
        /// - Avoid full reload for large DAGs.
        ///
        /// USE:
        /// - Called after retention eviction.
        /// </summary>
        Task WarmStepsAsync(
            string executionId,
            AiExecutionState state,
            IReadOnlyCollection<string> stepNames,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves a step by name.
        /// </summary>
        Task<AiStepState?> GetStepAsync(
            string executionId,
            string stepName,
            AiExecutionState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves lightweight step state without forcing full payload load when possible.
        ///
        /// PURPOSE:
        /// - Use hot state if available.
        /// - Use archived index metadata when the step was evicted.
        /// - Avoid loading full AiStepState payload for dependency/convergence checks.
        ///
        /// IMPORTANT:
        /// - Returned archived state may contain only identity/status metadata.
        /// - Use GetStepAsync when full result/config/output is required.
        /// </summary>
        Task<AiStepState?> GetStepStatusAsync(
            string executionId,
            string stepName,
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}