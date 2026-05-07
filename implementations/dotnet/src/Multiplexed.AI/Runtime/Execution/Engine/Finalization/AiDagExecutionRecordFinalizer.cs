using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution.Convergence;

namespace Multiplexed.AI.Runtime.Execution.Engine.Finalization
{
    /// <summary>
    /// Applies DAG convergence and authoritative persisted record snapshots
    /// to execution records.
    /// </summary>
    internal static class AiDagExecutionRecordFinalizer
    {
        /// <summary>
        /// Applies a convergence decision to the global execution record.
        /// </summary>
        /// <param name="record">The execution record to update.</param>
        /// <param name="convergence">The evaluated DAG convergence result.</param>
        /// <param name="state">The authoritative execution state.</param>
        public static void ApplyConvergenceToRecord(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(convergence);
            ArgumentNullException.ThrowIfNull(state);

            record.CompletedSteps = state.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            record.CurrentStep = string.Empty;

            switch (convergence.Status)
            {
                case AiExecutionStatus.Pending:
                    record.Status = AiExecutionStatus.Pending;
                    record.UpdatedAtUtc = DateTime.UtcNow;
                    break;

                case AiExecutionStatus.Running:
                    record.MarkRunning();
                    break;

                case AiExecutionStatus.Waiting:
                    record.MarkWaiting();
                    break;

                case AiExecutionStatus.Completed:
                    record.MarkCompleted();
                    break;

                case AiExecutionStatus.Failed:
                    record.MarkFailed();
                    break;

                case AiExecutionStatus.Cancelled:
                    record.MarkCancelled();
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported convergence status '{convergence.Status}'.");
            }
        }

        /// <summary>
        /// Applies an authoritative persisted record snapshot onto the current
        /// in-memory execution record projection.
        /// </summary>
        /// <param name="target">The record instance to update.</param>
        /// <param name="source">The authoritative persisted record.</param>
        public static void ApplyAuthoritativeRecord(
            AiExecutionRecord target,
            AiExecutionRecord source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            target.Status = source.Status;
            target.CompletedSteps = source.CompletedSteps;
            target.CurrentStep = source.CurrentStep;
            target.ExecutionStepKey = source.ExecutionStepKey;
            target.Version = source.Version;
            target.UpdatedAtUtc = source.UpdatedAtUtc;
            target.CompletedAtUtc = source.CompletedAtUtc;
        }
    }
}