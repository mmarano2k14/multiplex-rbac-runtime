using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Execution
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiExecutionMetrics"/>.
    ///
    /// PURPOSE:
    /// - Provide observability over execution lifecycle.
    /// - Track step execution activity across distributed workers.
    /// - Measure retry, recovery, claim, and finalization behavior.
    ///
    /// EXECUTION MODEL:
    /// - Supports DAG and sequential execution.
    /// - Works in multi-worker environments.
    /// - Tracks convergence through finalization attempts and conflicts.
    ///
    /// THREAD SAFETY:
    /// - This implementation is safe for singleton usage.
    /// - All scalar counters use atomic operations (<see cref="Interlocked"/>).
    /// - Dimensional counters use concurrent dictionaries.
    ///
    /// IMPORTANT:
    /// - This class is strictly observational.
    /// - It must not influence execution decisions, retries, recovery, or state transitions.
    /// </summary>
    public sealed class AiExecutionMetrics : IAiExecutionMetrics
    {
        private long _executionStartedCount;
        private long _executionCompletedCount;
        private long _executionFailedCount;

        private long _stepClaimedCount;
        private long _stepClaimMissCount;
        private long _stepCompletedCount;
        private long _stepFailedCount;
        private long _stepRetriedCount;
        private long _stepRecoveredCount;

        private long _finalizeAttemptCount;
        private long _finalizeSuccessCount;
        private long _finalizeConflictCount;

        private readonly ConcurrentDictionary<string, long> _retryByStep =
            new(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, long> _claimSuccessByStep =
            new(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, long> _recoveryByExecution =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public void RecordExecutionStarted(string executionId)
        {
            _ = executionId;
            Interlocked.Increment(ref _executionStartedCount);
        }

        /// <inheritdoc />
        public void RecordExecutionCompleted(string executionId)
        {
            _ = executionId;
            Interlocked.Increment(ref _executionCompletedCount);
        }

        /// <inheritdoc />
        public void RecordExecutionFailed(string executionId)
        {
            _ = executionId;
            Interlocked.Increment(ref _executionFailedCount);
        }

        /// <inheritdoc />
        public void RecordStepClaimed(string executionId, string stepId)
        {
            _ = executionId;

            Interlocked.Increment(ref _stepClaimedCount);
            IncrementDimension(_claimSuccessByStep, stepId);
        }

        /// <inheritdoc />
        public void RecordStepClaimMiss(string executionId)
        {
            _ = executionId;
            Interlocked.Increment(ref _stepClaimMissCount);
        }

        /// <inheritdoc />
        public void RecordStepCompleted(string executionId, string stepId)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _stepCompletedCount);
        }

        /// <inheritdoc />
        public void RecordStepFailed(string executionId, string stepId)
        {
            _ = executionId;
            _ = stepId;

            Interlocked.Increment(ref _stepFailedCount);
        }

        /// <inheritdoc />
        public void RecordStepRetried(string executionId, string stepId)
        {
            _ = executionId;

            Interlocked.Increment(ref _stepRetriedCount);
            IncrementDimension(_retryByStep, stepId);
        }

        /// <inheritdoc />
        public void RecordStepRecovered(string executionId, string stepId)
        {
            _ = stepId;

            Interlocked.Increment(ref _stepRecoveredCount);
            IncrementDimension(_recoveryByExecution, executionId);
        }

        /// <inheritdoc />
        public void RecordStepsRecovered(string executionId, int recoveredCount)
        {
            if (recoveredCount <= 0)
            {
                return;
            }

            Interlocked.Add(ref _stepRecoveredCount, recoveredCount);
            IncrementDimension(_recoveryByExecution, executionId, recoveredCount);
        }

        /// <inheritdoc />
        public void RecordFinalizeAttempt(string executionId)
        {
            _ = executionId;
            Interlocked.Increment(ref _finalizeAttemptCount);
        }

        /// <inheritdoc />
        public void RecordFinalizeSuccess(string executionId)
        {
            _ = executionId;
            Interlocked.Increment(ref _finalizeSuccessCount);
        }

        /// <inheritdoc />
        public void RecordFinalizeConflict(string executionId)
        {
            _ = executionId;
            Interlocked.Increment(ref _finalizeConflictCount);
        }

        /// <summary>
        /// Gets the total number of executions that have started.
        /// </summary>
        public long ExecutionStartedCount => Interlocked.Read(ref _executionStartedCount);

        /// <summary>
        /// Gets the total number of executions that completed successfully.
        /// </summary>
        public long ExecutionCompletedCount => Interlocked.Read(ref _executionCompletedCount);

        /// <summary>
        /// Gets the total number of executions that failed.
        /// </summary>
        public long ExecutionFailedCount => Interlocked.Read(ref _executionFailedCount);

        /// <summary>
        /// Gets the total number of successful step claims.
        /// </summary>
        public long StepClaimedCount => Interlocked.Read(ref _stepClaimedCount);

        /// <summary>
        /// Gets the total number of claim misses.
        /// </summary>
        public long StepClaimMissCount => Interlocked.Read(ref _stepClaimMissCount);

        /// <summary>
        /// Gets the total number of successfully completed steps.
        /// </summary>
        public long StepCompletedCount => Interlocked.Read(ref _stepCompletedCount);

        /// <summary>
        /// Gets the total number of failed step executions.
        /// </summary>
        public long StepFailedCount => Interlocked.Read(ref _stepFailedCount);

        /// <summary>
        /// Gets the total number of step retries.
        /// </summary>
        public long StepRetriedCount => Interlocked.Read(ref _stepRetriedCount);

        /// <summary>
        /// Gets the total number of recovered steps.
        /// </summary>
        public long StepRecoveredCount => Interlocked.Read(ref _stepRecoveredCount);

        /// <summary>
        /// Gets the total number of finalization attempts.
        /// </summary>
        public long FinalizeAttemptCount => Interlocked.Read(ref _finalizeAttemptCount);

        /// <summary>
        /// Gets the total number of successful finalizations.
        /// </summary>
        public long FinalizeSuccessCount => Interlocked.Read(ref _finalizeSuccessCount);

        /// <summary>
        /// Gets the total number of finalization conflicts.
        /// </summary>
        public long FinalizeConflictCount => Interlocked.Read(ref _finalizeConflictCount);

        /// <inheritdoc />
        public IReadOnlyDictionary<string, long> GetRetryByStep()
        {
            return Snapshot(_retryByStep);
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, long> GetClaimSuccessByStep()
        {
            return Snapshot(_claimSuccessByStep);
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, long> GetRecoveryByExecution()
        {
            return Snapshot(_recoveryByExecution);
        }

        private static void IncrementDimension(
            ConcurrentDictionary<string, long> target,
            string key,
            long amount = 1)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            target.AddOrUpdate(
                key,
                amount,
                (_, current) => current + amount);
        }

        private static IReadOnlyDictionary<string, long> Snapshot(
            ConcurrentDictionary<string, long> source)
        {
            return new ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>(source));
        }
    }
}