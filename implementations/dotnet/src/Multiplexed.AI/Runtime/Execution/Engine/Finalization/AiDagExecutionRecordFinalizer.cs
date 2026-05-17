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
            ApplyConvergenceToRecord(
                record,
                convergence,
                state,
                declaredStepNames: null);
        }

        /// <summary>
        /// Applies a convergence decision to the global execution record.
        /// </summary>
        /// <param name="record">The execution record to update.</param>
        /// <param name="convergence">The evaluated DAG convergence result.</param>
        /// <param name="state">The authoritative execution state.</param>
        /// <param name="declaredStepNames">The declared pipeline step names, when available.</param>
        public static void ApplyConvergenceToRecord(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            AiExecutionState state,
            IReadOnlyCollection<string>? declaredStepNames)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(convergence);
            ArgumentNullException.ThrowIfNull(state);

            record.CompletedSteps = BuildCompletedStepSnapshot(
                record,
                convergence,
                state,
                declaredStepNames);

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

        /// <summary>
        /// Builds the durable completed-step snapshot for the execution record.
        /// </summary>
        /// <param name="record">The current execution record.</param>
        /// <param name="convergence">The evaluated DAG convergence result.</param>
        /// <param name="state">The authoritative execution state.</param>
        /// <param name="declaredStepNames">The declared pipeline step names, when available.</param>
        /// <returns>The completed-step snapshot.</returns>
        /// <remarks>
        /// <para>
        /// Completed steps must be treated as durable execution history, not as a
        /// projection of the currently retained hot state only.
        /// </para>
        /// <para>
        /// Retention compaction and eviction may remove completed steps from
        /// <see cref="AiExecutionState.Steps"/> before terminal finalization. For that
        /// reason, this method preserves the existing record history and merges it with
        /// currently visible completed hot-state steps.
        /// </para>
        /// <para>
        /// When DAG convergence has already proven the execution completed, declared
        /// pipeline step names can be supplied by the caller as the authoritative DAG
        /// shape. This avoids losing completed steps that were evicted from hot state.
        /// </para>
        /// </remarks>
        private static List<string> BuildCompletedStepSnapshot(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            AiExecutionState state,
            IReadOnlyCollection<string>? declaredStepNames)
        {
            var completedSteps = record.CompletedSteps
                .Where(stepName => !string.IsNullOrWhiteSpace(stepName));

            var completedStepsFromState = state.Steps.Values
                .Where(step => step.IsCompleted)
                .Select(step => step.StepName)
                .Where(stepName => !string.IsNullOrWhiteSpace(stepName))
                .Select(stepName => stepName!);

            completedSteps = completedSteps.Concat(
                completedStepsFromState);

            if (convergence.Status == AiExecutionStatus.Completed &&
                declaredStepNames is not null)
            {
                completedSteps = completedSteps.Concat(
                    declaredStepNames.Where(stepName => !string.IsNullOrWhiteSpace(stepName))!);
            }

            return completedSteps
                .Distinct(StringComparer.Ordinal)
                .OrderBy(stepName => stepName, StringComparer.Ordinal)
                .ToList();
        }
    }
}