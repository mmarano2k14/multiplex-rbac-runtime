using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Memory
{
    /// <summary>
    /// Writes consolidated memories derived from execution outputs.
    ///
    /// PURPOSE:
    /// - Converts selected execution results into long-term memory records
    /// - Keeps memory creation separate from the execution ledger
    /// - Allows runtime outputs to become reusable knowledge without polluting execution state
    ///
    /// IMPORTANT:
    /// - This writer must never mutate DAG step state
    /// - This writer must never be required for deterministic replay
    /// - Memory records are derived artifacts, not execution truth
    /// </summary>
    public interface IAiMemoryWriter
    {
        /// <summary>
        /// Writes a memory derived from a step result when the result is eligible.
        /// </summary>
        Task<AiConsolidatedMemoryRecord?> WriteFromStepResultAsync(
            AiExecutionRecord record,
            string stepName,
            AiStepResult result,
            string scope,
            CancellationToken cancellationToken = default);
    }
}