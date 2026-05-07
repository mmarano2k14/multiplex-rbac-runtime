using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Convergence;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Models;
using Multiplexed.AI.Runtime.Execution.Engine.Retention;

namespace Multiplexed.AI.Runtime.Execution.Engine.Finalization
{
    /// <summary>
    /// Coordinates distributed execution convergence persistence and terminal finalization.
    /// </summary>
    public sealed class AiDagExecutionFinalizationService
    {
        private readonly IAiDagExecutionEngineServices _engineServices;
        private readonly AiDagRetentionCoordinator _retentionCoordinator;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionFinalizationService"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services.
        /// </param>
        /// <param name="retentionCoordinator">
        /// The retention coordinator.
        /// </param>
        public AiDagExecutionFinalizationService(
            IAiDagExecutionEngineServices engineServices,
            AiDagRetentionCoordinator retentionCoordinator)
        {
            _engineServices = engineServices
                ?? throw new ArgumentNullException(nameof(engineServices));

            _retentionCoordinator = retentionCoordinator
                ?? throw new ArgumentNullException(nameof(retentionCoordinator));
        }

        /// <summary>
        /// Persists a converged execution record in a distributed-safe manner.
        /// </summary>
        /// <param name="record">
        /// The execution record projection to persist.
        /// </param>
        /// <param name="convergence">
        /// The evaluated convergence result.
        /// </param>
        /// <param name="expectedStepKey">
        /// The optimistic execution step key.
        /// </param>
        /// <param name="state">
        /// The authoritative execution state.
        /// </param>
        /// <param name="resolvedPipeline">
        /// The resolved pipeline.
        /// </param>
        /// <param name="buildExecutionContext">
        /// Factory used to build execution contexts.
        /// </param>
        /// <param name="persistAsync">
        /// Fallback persistence delegate for non-distributed execution.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task PersistDistributedConvergedRecordAsync(
            AiExecutionRecord record,
            AiDagExecutionConvergenceResult convergence,
            string expectedStepKey,
            AiExecutionState state,
            ResolvedAiPipeline resolvedPipeline,
            Func<AiExecutionRecord, AiExecutionState, CancellationToken, AiExecutionContext> buildExecutionContext,
            Func<AiExecutionRecord, string, AiExecutionState, CancellationToken, Task> persistAsync,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(convergence);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedStepKey);
            ArgumentNullException.ThrowIfNull(state);

            var utcNow = DateTime.UtcNow;

            if (_engineServices.DagStore is null)
            {
                record.TouchVersion();
                record.RenewExecutionStepKey();

                await persistAsync(
                    record,
                    expectedStepKey,
                    state,
                    cancellationToken);

                return;
            }

            var completedSteps = record.CompletedSteps
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (convergence.IsTerminal)
            {
                _engineServices.Logger.Engine.LogInformation(
                    $"[AI DAG] Finalization attempt. ExecutionId='{record.ExecutionId}', Status='{convergence.Status}', ExpectedStepKey='{expectedStepKey}'.");

                var retentionStep = resolvedPipeline.Steps.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Pipeline '{resolvedPipeline.Name}' does not contain any resolved step for retention context creation.");

                var executionContext = buildExecutionContext(
                    record,
                    state,
                    cancellationToken);

                var retentionStepContext = new AiStepExecutionContext(
                    executionContext,
                    retentionStep);

                await _retentionCoordinator.ApplyRetentionPersistAndWarmAsync(
                    record.ExecutionId,
                    state,
                    retentionStepContext,
                    cancellationToken);

                var request = new AiDagExecutionFinalizationRequest
                {
                    ExecutionId = record.ExecutionId,
                    ExpectedExecutionStepKey = expectedStepKey,
                    Status = convergence.Status,
                    CompletedAtUtc = utcNow,
                    CompletedSteps = completedSteps,
                    CurrentStep = string.Empty,
                    WorkerId = Environment.MachineName
                };

                var success =
                    await _engineServices.ObservabilityService.Tracer.TraceStorageAsync(
                        new AiStorageTraceContext
                        {
                            ExecutionId = record.ExecutionId,
                            Backend = "Redis",
                            Operation = "TryFinalizeExecution"
                        },
                        async trace =>
                        {
                            var result = await _engineServices.DagStore.TryFinalizeExecutionAsync(
                                request,
                                cancellationToken);

                            trace.SetTag("finalized", result);
                            trace.SetTag("status", convergence.Status.ToString());
                            trace.SetTag("workerId", Environment.MachineName);
                            trace.SetTag("expectedStepKey", expectedStepKey);
                            trace.SetTag("completedSteps", completedSteps.Count);

                            return result;
                        });

                if (!success)
                {
                    _engineServices.Logger.Engine.FinalizationRaceLost(
                        record.ExecutionId,
                        convergence.Status);

                    var refreshed = await _engineServices.DagStore.GetRecordAsync(
                        record.ExecutionId,
                        cancellationToken);

                    if (refreshed is not null)
                    {
                        AiDagExecutionRecordFinalizer.ApplyAuthoritativeRecord(
                            record,
                            refreshed);
                    }

                    return;
                }

                _engineServices.Logger.Engine.FinalizationSucceeded(
                    record.ExecutionId,
                    convergence.Status);

                var updated = await _engineServices.DagStore.GetRecordAsync(
                    record.ExecutionId,
                    cancellationToken);

                if (updated is not null)
                {
                    AiDagExecutionRecordFinalizer.ApplyAuthoritativeRecord(
                        record,
                        updated);
                }

                return;
            }

            record.TouchVersion();
            record.RenewExecutionStepKey();

            await _engineServices.DagStore.SaveRecordAsync(
                record,
                cancellationToken);
        }
    }
}