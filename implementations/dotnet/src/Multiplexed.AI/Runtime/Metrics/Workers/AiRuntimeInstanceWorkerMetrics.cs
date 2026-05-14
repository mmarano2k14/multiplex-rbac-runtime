using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using Multiplexed.Abstractions.AI.Metrics.Workers;

namespace Multiplexed.AI.Runtime.Metrics.Workers
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRuntimeInstanceWorkerMetrics"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This implementation is safe for singleton usage. Scalar counters use atomic
    /// operations and dimensional counters use concurrent dictionaries.
    /// </para>
    /// <para>
    /// Metrics are observational only and must not influence runtime execution,
    /// scheduling, retries, throttling, or convergence.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimeInstanceWorkerMetrics : IAiRuntimeInstanceWorkerMetrics
    {
        private long _workerStartedCount;
        private long _workerCycleCount;
        private long _workerIdleCount;
        private long _workerRaceLostCount;
        private long _workerTerminalCount;
        private long _workerCancelledCount;
        private long _workerMaxCyclesExceededCount;

        private readonly ConcurrentDictionary<string, long> _cyclesByRuntimeInstance =
            new(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, long> _terminalByStatus =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, long> _raceLossByRuntimeInstance =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public void RecordWorkerStarted(
            string executionId,
            string runtimeInstanceId)
        {
            _ = executionId;
            Interlocked.Increment(ref _workerStartedCount);
            IncrementDimension(_cyclesByRuntimeInstance, runtimeInstanceId, 0);
        }

        /// <inheritdoc />
        public void RecordWorkerCycle(
            string executionId,
            string runtimeInstanceId)
        {
            _ = executionId;
            Interlocked.Increment(ref _workerCycleCount);
            IncrementDimension(_cyclesByRuntimeInstance, runtimeInstanceId);
        }

        /// <inheritdoc />
        public void RecordWorkerIdle(
            string executionId,
            string runtimeInstanceId)
        {
            _ = executionId;
            _ = runtimeInstanceId;
            Interlocked.Increment(ref _workerIdleCount);
        }

        /// <inheritdoc />
        public void RecordWorkerRaceLost(
            string executionId,
            string runtimeInstanceId)
        {
            _ = executionId;
            Interlocked.Increment(ref _workerRaceLostCount);
            IncrementDimension(_raceLossByRuntimeInstance, runtimeInstanceId);
        }

        /// <inheritdoc />
        public void RecordWorkerTerminal(
            string executionId,
            string runtimeInstanceId,
            string status)
        {
            _ = executionId;
            _ = runtimeInstanceId;
            Interlocked.Increment(ref _workerTerminalCount);
            IncrementDimension(_terminalByStatus, status);
        }

        /// <inheritdoc />
        public void RecordWorkerCancelled(
            string executionId,
            string runtimeInstanceId)
        {
            _ = executionId;
            _ = runtimeInstanceId;
            Interlocked.Increment(ref _workerCancelledCount);
        }

        /// <inheritdoc />
        public void RecordWorkerMaxCyclesExceeded(
            string executionId,
            string runtimeInstanceId,
            int maxCycles)
        {
            _ = executionId;
            _ = runtimeInstanceId;
            _ = maxCycles;
            Interlocked.Increment(ref _workerMaxCyclesExceededCount);
        }

        /// <summary>
        /// Gets the total number of worker starts.
        /// </summary>
        public long WorkerStartedCount => Interlocked.Read(ref _workerStartedCount);

        /// <summary>
        /// Gets the total number of worker cycles.
        /// </summary>
        public long WorkerCycleCount => Interlocked.Read(ref _workerCycleCount);

        /// <summary>
        /// Gets the total number of idle worker cycles.
        /// </summary>
        public long WorkerIdleCount => Interlocked.Read(ref _workerIdleCount);

        /// <summary>
        /// Gets the total number of distributed race losses observed by workers.
        /// </summary>
        public long WorkerRaceLostCount => Interlocked.Read(ref _workerRaceLostCount);

        /// <summary>
        /// Gets the total number of terminal executions observed by workers.
        /// </summary>
        public long WorkerTerminalCount => Interlocked.Read(ref _workerTerminalCount);

        /// <summary>
        /// Gets the total number of worker cancellations.
        /// </summary>
        public long WorkerCancelledCount => Interlocked.Read(ref _workerCancelledCount);

        /// <summary>
        /// Gets the total number of max-cycle exits.
        /// </summary>
        public long WorkerMaxCyclesExceededCount => Interlocked.Read(ref _workerMaxCyclesExceededCount);

        /// <inheritdoc />
        public IReadOnlyDictionary<string, long> GetCyclesByRuntimeInstance()
        {
            return Snapshot(_cyclesByRuntimeInstance);
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, long> GetTerminalByStatus()
        {
            return Snapshot(_terminalByStatus);
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, long> GetRaceLossByRuntimeInstance()
        {
            return Snapshot(_raceLossByRuntimeInstance);
        }

        private static void IncrementDimension(
            ConcurrentDictionary<string, long> target,
            string key,
            long amount = 1)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (amount == 0)
            {
                target.TryAdd(key, 0);
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