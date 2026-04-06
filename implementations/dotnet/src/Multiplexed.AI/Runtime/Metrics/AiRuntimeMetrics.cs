using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics
{
    /// <summary>
    /// Default in-memory implementation of IAiRuntimeMetrics.
    ///
    /// Design goals:
    /// - Thread-safe (multi-worker safe)
    /// - Lock-free where possible (Interlocked / ConcurrentDictionary)
    /// - Low overhead (safe for hot paths like claim/retry)
    /// - Snapshot-friendly for debug endpoints
    ///
    /// This implementation is intentionally simple.
    /// It can later be replaced by a Prometheus/OpenTelemetry adapter.
    /// </summary>
    public sealed class AiRuntimeMetrics : IAiRuntimeMetrics
    {
        // ===== GLOBAL COUNTERS =====

        private long _finalizeAttempts;
        private long _finalizeSuccess;
        private long _claimMiss;

        // ===== DIMENSIONAL METRICS =====

        /// <summary>
        /// Retry count per step (StepName → count).
        /// </summary>
        private readonly ConcurrentDictionary<string, long> _retryByStep =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Successful claims per step (StepName → count).
        /// </summary>
        private readonly ConcurrentDictionary<string, long> _claimSuccessByStep =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Recovery count per execution (ExecutionId → recovered steps count).
        /// </summary>
        private readonly ConcurrentDictionary<string, long> _recoveryByExecution =
            new(StringComparer.Ordinal);

        // ===== WRITE API =====

        public void IncrementRetry(string stepName)
        {
            if (string.IsNullOrWhiteSpace(stepName))
                return;

            _retryByStep.AddOrUpdate(
                stepName,
                1,
                (_, current) => current + 1);
        }

        public void IncrementRecovery(string executionId, int recoveredCount)
        {
            if (string.IsNullOrWhiteSpace(executionId) || recoveredCount <= 0)
                return;

            _recoveryByExecution.AddOrUpdate(
                executionId,
                recoveredCount,
                (_, current) => current + recoveredCount);
        }

        public void IncrementFinalizeAttempt()
        {
            Interlocked.Increment(ref _finalizeAttempts);
        }

        public void IncrementFinalizeSuccess()
        {
            Interlocked.Increment(ref _finalizeSuccess);
        }

        public void IncrementClaimSuccess(string stepName)
        {
            if (string.IsNullOrWhiteSpace(stepName))
                return;

            _claimSuccessByStep.AddOrUpdate(
                stepName,
                1,
                (_, current) => current + 1);
        }

        public void IncrementClaimMiss()
        {
            Interlocked.Increment(ref _claimMiss);
        }

        // ===== READ API =====

        /// <summary>
        /// Total number of finalization attempts.
        /// </summary>
        public long GetFinalizeAttempts()
            => Interlocked.Read(ref _finalizeAttempts);

        /// <summary>
        /// Total number of successful finalizations.
        /// </summary>
        public long GetFinalizeSuccess()
            => Interlocked.Read(ref _finalizeSuccess);

        /// <summary>
        /// Total number of claim misses.
        /// </summary>
        public long GetClaimMiss()
            => Interlocked.Read(ref _claimMiss);

        /// <summary>
        /// Snapshot of retry counts per step.
        /// </summary>
        public IReadOnlyDictionary<string, long> GetRetryByStep()
            => new ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>(_retryByStep));

        /// <summary>
        /// Snapshot of successful claims per step.
        /// </summary>
        public IReadOnlyDictionary<string, long> GetClaimSuccessByStep()
            => new ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>(_claimSuccessByStep));

        /// <summary>
        /// Snapshot of recovered steps per execution.
        /// </summary>
        public IReadOnlyDictionary<string, long> GetRecoveryByExecution()
            => new ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>(_recoveryByExecution));
    }
}