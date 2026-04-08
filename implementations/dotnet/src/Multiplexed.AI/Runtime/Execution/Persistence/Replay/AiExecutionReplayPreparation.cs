using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Prepares a persisted execution snapshot for safe runtime replay.
    ///
    /// Purpose:
    /// - remove transient runtime ownership data that must never survive replay
    /// - convert in-flight step states into replay-safe states
    /// - ensure the restored execution can be picked up again by the engine
    ///
    /// Core safety rule:
    /// - replay must never restore an active worker claim, lease, or ownership token
    ///
    /// Current behavior:
    /// - non-terminal execution records are moved back to Running
    /// - running steps are converted to Ready
    /// - terminal step states are preserved
    /// - waiting-for-retry is preserved
    /// - transient claim metadata is cleared
    /// </summary>
    public static class AiExecutionReplayPreparation
    {
        /// <summary>
        /// Prepares the provided execution record and state for replay.
        /// </summary>
        /// <param name="record">
        /// The execution record to normalize before restore.
        /// </param>
        /// <param name="state">
        /// The execution state to normalize before restore.
        /// </param>
        public static void Prepare(AiExecutionRecord? record, AiExecutionState? state)
        {
            PrepareRecord(record);
            PrepareState(state);
        }

        /// <summary>
        /// Normalizes the execution record so it can safely re-enter the runtime.
        /// Terminal records are left unchanged.
        /// </summary>
        private static void PrepareRecord(AiExecutionRecord? record)
        {
            if (record is null)
            {
                return;
            }

            if (record.Status == AiExecutionStatus.Completed ||
                record.Status == AiExecutionStatus.Failed ||
                record.Status == AiExecutionStatus.Cancelled)
            {
                return;
            }

            record.Status = AiExecutionStatus.Running;

            // Clear record-level runtime projection fields that may no longer
            // represent a valid in-flight step after replay.
            record.CurrentStep = null;
            record.ExecutionStepKey = null;
        }

        /// <summary>
        /// Normalizes all persisted step states for replay.
        /// </summary>
        private static void PrepareState(AiExecutionState? state)
        {
            if (state is null || state.Steps is null || state.Steps.Count == 0)
            {
                return;
            }

            foreach (var pair in state.Steps)
            {
                PrepareStep(pair.Value);
            }
        }

        /// <summary>
        /// Normalizes a single step state for replay.
        /// Any in-flight step is converted back to a schedulable state.
        /// </summary>
        private static void PrepareStep(AiStepState? step)
        {
            if (step is null)
            {
                return;
            }

            if (step.Status == AiStepExecutionStatus.Running)
            {
                step.Status = AiStepExecutionStatus.Ready;
            }

            ClearTransientClaims(step);
        }

        /// <summary>
        /// Removes transient claim and ownership data that must never be restored
        /// from a persisted snapshot.
        /// </summary>
        private static void ClearTransientClaims(AiStepState step)
        {
            step.ClaimToken = null;
            step.ClaimedBy = null;
            step.ClaimedAtUtc = null;
        }
    }
}