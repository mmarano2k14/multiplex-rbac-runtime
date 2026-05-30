using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Observability.Metrics.Workers
{
    /// <summary>
    /// Records metrics for runtime instance worker execution loops.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These metrics observe the higher-level worker loop that repeatedly advances
    /// AI executions through engine batch cycles.
    /// </para>
    /// <para>
    /// Worker metrics are distinct from execution metrics. Execution metrics describe
    /// step claims, completions, retries, failures, recovery, and finalization. Worker
    /// metrics describe orchestration-loop behavior such as worker starts, cycles,
    /// idle waits, race losses, terminal completions, cancellations, and max-cycle exits.
    /// </para>
    /// <para>
    /// Metrics are observational only and must never drive execution decisions.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeInstanceWorkerMetrics
    {
        /// <summary>
        /// Records that a runtime instance worker started processing an execution.
        /// </summary>
        void RecordWorkerStarted(
            string executionId,
            string runtimeInstanceId);

        /// <summary>
        /// Records that a runtime instance worker executed one worker cycle.
        /// </summary>
        void RecordWorkerCycle(
            string executionId,
            string runtimeInstanceId);

        /// <summary>
        /// Records that a runtime instance worker observed an idle non-terminal cycle.
        /// </summary>
        void RecordWorkerIdle(
            string executionId,
            string runtimeInstanceId);

        /// <summary>
        /// Records that a runtime instance worker lost an expected distributed race.
        /// </summary>
        void RecordWorkerRaceLost(
            string executionId,
            string runtimeInstanceId);

        /// <summary>
        /// Records that a runtime instance worker observed a terminal execution.
        /// </summary>
        void RecordWorkerTerminal(
            string executionId,
            string runtimeInstanceId,
            string status);

        /// <summary>
        /// Records that a runtime instance worker was cancelled.
        /// </summary>
        void RecordWorkerCancelled(
            string executionId,
            string runtimeInstanceId);

        /// <summary>
        /// Records that a runtime instance worker stopped because the maximum cycle count was exceeded.
        /// </summary>
        void RecordWorkerMaxCyclesExceeded(
            string executionId,
            string runtimeInstanceId,
            int maxCycles);

        /// <summary>
        /// Gets a snapshot of worker cycle counts grouped by runtime instance id.
        /// </summary>
        IReadOnlyDictionary<string, long> GetCyclesByRuntimeInstance();

        /// <summary>
        /// Gets a snapshot of worker terminal counts grouped by execution status.
        /// </summary>
        IReadOnlyDictionary<string, long> GetTerminalByStatus();

        /// <summary>
        /// Gets a snapshot of distributed race loss counts grouped by runtime instance id.
        /// </summary>
        IReadOnlyDictionary<string, long> GetRaceLossByRuntimeInstance();
    }
}