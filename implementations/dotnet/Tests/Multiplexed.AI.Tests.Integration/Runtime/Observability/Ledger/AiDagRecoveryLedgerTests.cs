using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Control;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Observability.Ledger;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;
using NSubstitute;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Observability.Ledger
{
    /// <summary>
    /// Tests decision ledger recording around DAG step recovery.
    /// </summary>
    public sealed class AiDagRecoveryLedgerTests
    {
        /// <summary>
        /// Verifies that recovering timed-out steps records recovery detected, applied,
        /// and one step-recovered event per recovered step name.
        /// </summary>
        [Fact]
        public async Task ClaimNextAsync_WhenTimedOutStepsAreRecovered_ShouldRecordRecoveryLedgerEventsWithStepNames()
        {
            var executionId = "exec-recovery-ledger";
            var pipelineKey = "test-pipeline:v1";
            var workerId = "worker-1";
            var recoveredStepName = "step-a";

            var ledger = new InMemoryAiDecisionLedger();

            var services = CreateServices(ledger);

            var beforeState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            beforeState.Steps[recoveredStepName] = new AiStepState
            {
                StepName = recoveredStepName,
                Status = AiStepExecutionStatus.Running,
                RecoveryCount = 0
            };

            var afterState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            afterState.Steps[recoveredStepName] = new AiStepState
            {
                StepName = recoveredStepName,
                Status = AiStepExecutionStatus.Ready,
                RecoveryCount = 1
            };

            services.DagStore!
                .GetStateAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(
                    beforeState,
                    afterState,
                    afterState);

            services.DagStore!
                .RecoverTimedOutStepsAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(1);

            services.DagStore!
                .GetReadyStepsAsync(
                    executionId,
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(Array.Empty<AiClaimedStep>());

            var pipeline = CreatePipeline(recoveredStepName);

            var service = new AiDagStepClaimService(services);

            var claimed = await service.ClaimNextAsync(
                executionId,
                pipeline,
                pipelineKey,
                workerId,
                CancellationToken.None);

            Assert.Null(claimed);

            var entries = await ledger.GetByExecutionAsync(executionId);

            Assert.Contains(entries, entry =>
                entry.Category == AiDecisionLedgerCategory.Recovery &&
                entry.EventType == AiDecisionLedgerEvents.Recovery.Detected &&
                entry.Outcome == AiDecisionLedgerOutcome.Started &&
                entry.CorrelationContext.StepKey == "_recovery");

            Assert.Contains(entries, entry =>
                entry.Category == AiDecisionLedgerCategory.Recovery &&
                entry.EventType == AiDecisionLedgerEvents.Recovery.Applied &&
                entry.Outcome == AiDecisionLedgerOutcome.Applied &&
                entry.CorrelationContext.StepKey == "_recovery");

            Assert.Contains(entries, entry =>
                entry.Category == AiDecisionLedgerCategory.Recovery &&
                entry.EventType == AiDecisionLedgerEvents.Recovery.StepRecovered &&
                entry.Outcome == AiDecisionLedgerOutcome.Applied &&
                entry.CorrelationContext.StepKey == recoveredStepName);

            Assert.Contains(entries, entry =>
                entry.EventType == AiDecisionLedgerEvents.Recovery.StepRecovered &&
                entry.Metadata is not null &&
                entry.Metadata.TryGetValue("step.name", out var stepName) &&
                stepName == recoveredStepName);

            Assert.All(entries.Where(entry => entry.Category == AiDecisionLedgerCategory.Recovery), entry =>
            {
                Assert.Equal(executionId, entry.CorrelationContext.ExecutionId);
                Assert.Equal(pipelineKey, entry.CorrelationContext.PipelineName);
                Assert.Equal(workerId, entry.CorrelationContext.WorkerId);
                Assert.Equal(workerId, entry.CorrelationContext.RuntimeInstanceId);
            });
        }

        /// <summary>
        /// Verifies that batch claim recovery also records recovered step names.
        /// </summary>
        [Fact]
        public async Task ClaimBatchAsync_WhenTimedOutStepsAreRecovered_ShouldRecordRecoveryLedgerEventsWithStepNames()
        {
            var executionId = "exec-batch-recovery-ledger";
            var pipelineKey = "test-pipeline:v1";
            var workerId = "worker-1";
            var recoveredStepName = "step-a";

            var ledger = new InMemoryAiDecisionLedger();

            var services = CreateServices(ledger);

            var beforeState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            beforeState.Steps[recoveredStepName] = new AiStepState
            {
                StepName = recoveredStepName,
                Status = AiStepExecutionStatus.Running,
                RecoveryCount = 0
            };

            var afterState = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            afterState.Steps[recoveredStepName] = new AiStepState
            {
                StepName = recoveredStepName,
                Status = AiStepExecutionStatus.Ready,
                RecoveryCount = 1
            };

            services.DagStore!
                .GetStateAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(
                    beforeState,
                    afterState,
                    afterState);

            services.DagStore!
                .RecoverTimedOutStepsAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(1);

            services.DagStore!
                .GetReadyStepsAsync(
                    executionId,
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(Array.Empty<AiClaimedStep>());

            var pipeline = CreatePipeline(recoveredStepName);

            var service = new AiDagStepClaimService(services);

            var claimed = await service.ClaimBatchAsync(
                executionId,
                pipeline,
                pipelineKey,
                workerId,
                maxSteps: 4,
                CancellationToken.None);

            Assert.Empty(claimed);

            var entries = await ledger.GetByExecutionAsync(executionId);

            Assert.Contains(entries, entry =>
                entry.Category == AiDecisionLedgerCategory.Recovery &&
                entry.EventType == AiDecisionLedgerEvents.Recovery.Detected &&
                entry.Outcome == AiDecisionLedgerOutcome.Started);

            Assert.Contains(entries, entry =>
                entry.Category == AiDecisionLedgerCategory.Recovery &&
                entry.EventType == AiDecisionLedgerEvents.Recovery.Applied &&
                entry.Outcome == AiDecisionLedgerOutcome.Applied);

            Assert.Contains(entries, entry =>
                entry.Category == AiDecisionLedgerCategory.Recovery &&
                entry.EventType == AiDecisionLedgerEvents.Recovery.StepRecovered &&
                entry.Outcome == AiDecisionLedgerOutcome.Applied &&
                entry.CorrelationContext.StepKey == recoveredStepName);
        }

        /// <summary>
        /// Verifies that no recovery ledger events are recorded when no timed-out steps are recovered.
        /// </summary>
        [Fact]
        public async Task ClaimNextAsync_WhenNoStepsAreRecovered_ShouldNotRecordRecoveryLedgerEvents()
        {
            var executionId = "exec-no-recovery-ledger";
            var pipelineKey = "test-pipeline:v1";
            var workerId = "worker-1";
            var stepName = "step-a";

            var ledger = new InMemoryAiDecisionLedger();

            var services = CreateServices(ledger);

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline"
            };

            state.Steps[stepName] = new AiStepState
            {
                StepName = stepName,
                Status = AiStepExecutionStatus.Ready,
                RecoveryCount = 0
            };

            services.DagStore!
                .GetStateAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(state);

            services.DagStore!
                .RecoverTimedOutStepsAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(0);

            services.DagStore!
                .GetReadyStepsAsync(
                    executionId,
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(Array.Empty<AiClaimedStep>());

            var pipeline = CreatePipeline(stepName);

            var service = new AiDagStepClaimService(services);

            var claimed = await service.ClaimNextAsync(
                executionId,
                pipeline,
                pipelineKey,
                workerId,
                CancellationToken.None);

            Assert.Null(claimed);

            var entries = await ledger.GetByExecutionAsync(executionId);

            Assert.DoesNotContain(entries, entry =>
                entry.Category == AiDecisionLedgerCategory.Recovery);
        }

        private static IAiDagExecutionEngineServices CreateServices(
            InMemoryAiDecisionLedger ledger)
        {
            var services = Substitute.For<IAiDagExecutionEngineServices>();
            var dagStore = Substitute.For<IAiDagExecutionStore>();
            var observability = Substitute.For<IAiRuntimeObservability>();
            var runtimeMetrics = Substitute.For<IAiRuntimeMetrics>();
            var logger = Substitute.For<IAiRuntimeLogger>();
            var controlGate = Substitute.For<IAiExecutionControlGate>();

            var recorder = new DefaultAiDecisionLedgerRecorder(
                ledger,
                Options.Create(new AiDecisionLedgerRecorderOptions
                {
                    WriteMode = AiDecisionLedgerWriteMode.Strict,
                    StorageMode = AiDecisionLedgerStorageMode.InMemory
                }),
                NullLogger<DefaultAiDecisionLedgerRecorder>.Instance);

            observability.Tracer.Returns(new PassthroughAiRuntimeTracer());
            observability.Ledger.Returns(recorder);
            observability.Metrics.Returns(runtimeMetrics);

            controlGate.CheckBeforeAdvanceAsync(
                    Arg.Any<string>(),
                    Arg.Any<CancellationToken>())
                .Returns(new AiExecutionControlDecision
                {
                    CanContinue = true,
                    ShouldCancel = false,
                    Status = AiExecutionControlStatus.Running
                });

            services.DagStore.Returns(dagStore);
            services.ObservabilityService.Returns(observability);
            services.Logger.Returns(logger);
            services.ExecutionControlGate.Returns(controlGate);

            return services;
        }

        private static ResolvedAiPipeline CreatePipeline(
            string stepName)
        {
            return new ResolvedAiPipeline
            {
                Name = "test-pipeline",
                Version = "v1",
                ExecutionMode = AiExecutionMode.Dag,
                Config = new Dictionary<string, object?>(),
                Steps =
                [
                    new ResolvedAiPipelineStep
                    {
                        Name = stepName,
                        StepKey = "debug.pass",
                        Config = new Dictionary<string, object?>(),
                        DependsOn = Array.Empty<string>()
                    }
                ]
            };
        }

        private sealed class PassthroughAiRuntimeTracer : IAiRuntimeTracer
        {
            public IAiTraceScope StartExecution(AiExecutionTraceContext context)
            {
                return Substitute.For<IAiTraceScope>();
            }

            public IAiTraceScope StartResolver(AiResolverTraceContext context)
            {
                return Substitute.For<IAiTraceScope>();
            }

            public IAiTraceScope StartRetention(AiRetentionTraceContext context)
            {
                return Substitute.For<IAiTraceScope>();
            }

            public IAiTraceScope StartStep(AiStepTraceContext context)
            {
                return Substitute.For<IAiTraceScope>();
            }

            public IAiTraceScope StartStorage(AiStorageTraceContext context)
            {
                return Substitute.For<IAiTraceScope>();
            }

            public Task<TResult> TraceStorageAsync<TResult>(
                AiStorageTraceContext context,
                Func<IAiTraceScope, Task<TResult>> action,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scope = Substitute.For<IAiTraceScope>();

                return action(scope);
            }

            public Task<TResult> TraceRetentionAsync<TResult>(
                AiRetentionTraceContext context,
                Func<IAiTraceScope, Task<TResult>> action,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scope = Substitute.For<IAiTraceScope>();

                return action(scope);
            }

            public Task<TResult> TraceStepAsync<TResult>(
                AiStepTraceContext context,
                Func<Task<TResult>> action,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                return action();
            }
        }
    }
}