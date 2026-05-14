using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.AI.Runtime.Execution.Engine.Core;

namespace Multiplexed.AI.Runtime.Execution.Engine.Steps
{
    /// <summary>
    /// Executes already-claimed DAG steps.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Centralizes physical DAG step execution.
    /// - Keeps distributed orchestration separated from step execution logic.
    /// - Allows batch and distributed runners to reuse the same execution behavior.
    ///
    /// IMPORTANT:
    /// - This class does not claim steps.
    /// - This class does not finalize steps.
    /// - This class releases distributed concurrency capacity after execution.
    ///
    /// DISTRIBUTED OWNERSHIP:
    /// - Step ownership is represented by the claim token.
    /// - Concurrency ownership is represented by a deterministic lease id.
    /// - Claim validation remains enforced by the DAG store.
    /// </remarks>
    public sealed class AiDagClaimedStepExecutor
    {
        private static readonly IAiConcurrencyDefinitionResolver ConcurrencyDefinitionResolver =
            new DefaultAiConcurrencyDefinitionResolver();

        private readonly IAiDagExecutionEngineServices _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagClaimedStepExecutor"/> class.
        /// </summary>
        /// <param name="services">The DAG execution engine services.</param>
        public AiDagClaimedStepExecutor(
            IAiDagExecutionEngineServices services)
        {
            _services = services
                ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Executes an already-claimed DAG step and releases its concurrency slot afterwards.
        /// </summary>
        /// <param name="record">The execution record.</param>
        /// <param name="state">The execution state.</param>
        /// <param name="resolvedPipeline">The resolved pipeline definition.</param>
        /// <param name="claimedStep">The claimed step to execute.</param>
        /// <param name="buildExecutionContext">Factory used to build execution contexts.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The step execution result.</returns>
        public async Task<AiStepResult> ExecuteAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            ResolvedAiPipeline resolvedPipeline,
            AiClaimedStep claimedStep,
            Func<AiExecutionRecord, AiExecutionState, CancellationToken, AiExecutionContext> buildExecutionContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(resolvedPipeline);
            ArgumentNullException.ThrowIfNull(claimedStep);
            ArgumentNullException.ThrowIfNull(buildExecutionContext);

            var resolvedStep = resolvedPipeline.Steps
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Name,
                        claimedStep.StepName,
                        StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Claimed step '{claimedStep.StepName}' was not found in resolved pipeline '{resolvedPipeline.Name}'.");

            var executionContext = buildExecutionContext(
                record,
                state,
                cancellationToken);

            var stepContext = new AiStepExecutionContext(
                executionContext,
                resolvedStep);

            var stepState = state.Steps.TryGetValue(
                claimedStep.StepName,
                out var existingStepState)
                ? existingStepState
                : null;

            var concurrencyDefinition = stepState is not null
                ? ConcurrencyDefinitionResolver.Resolve(stepState)
                : new AiConcurrencyDefinition
                {
                    Enabled = false
                };

            var concurrencyContext = new AiConcurrencyContext
            {
                ExecutionId = record.ExecutionId,
                PipelineKey = $"{resolvedPipeline.Name}:{resolvedPipeline.Version}",
                StepId = claimedStep.StepName,
                StepKey = resolvedStep.StepKey,
                RuntimeInstanceId = _services.RuntimeInstanceIdentity.RuntimeInstanceId,
                LeaseId = $"{record.ExecutionId}:{claimedStep.StepName}:{_services.RuntimeInstanceIdentity.RuntimeInstanceId}"
            };

            try
            {
                try
                {
                    return await _services.ObservabilityService.Tracer.TraceStepAsync(
                        new AiStepTraceContext
                        {
                            ExecutionId = record.ExecutionId,
                            StepId = claimedStep.StepName,
                            StepType = resolvedStep.Step.GetType().Name,
                            Status = "Running",
                            RetryCount = stepState?.RetryState?.RetryCount ?? 0,
                            RecoveryCount = stepState?.RecoveryCount ?? 0,
                            WorkerId = _services.RuntimeInstanceIdentity.RuntimeInstanceId,
                            ClaimToken = claimedStep.ClaimToken
                        },
                        async () =>
                        {
                            var result = await resolvedStep.Step.ExecuteAsync(
                                stepContext,
                                cancellationToken).ConfigureAwait(false);

                            await _services.PayloadCompactor.CompactAsync(
                                result,
                                cancellationToken).ConfigureAwait(false);

                            return result;
                        }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _services.Logger.Engine.LogWarning(
                        $"[AI DAG] Step exception converted to failed result. ExecutionId='{record.ExecutionId}', StepName='{claimedStep.StepName}', ClaimToken='{claimedStep.ClaimToken}', Error='{ex.Message}'.");

                    return AiStepResult.Fail(
                        ex.Message);
                }
            }
            finally
            {
                await _services.ConcurrencyGate.ReleaseAsync(
                    concurrencyContext,
                    concurrencyDefinition,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}