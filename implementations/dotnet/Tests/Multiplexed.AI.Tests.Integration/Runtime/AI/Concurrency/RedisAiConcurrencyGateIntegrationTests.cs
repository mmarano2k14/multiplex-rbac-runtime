using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.AI.Concurrency;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Execution.Engine.Steps;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.AI.Concurrency
{
    /// <summary>
    /// Integration tests for the Redis-backed AI concurrency gate.
    /// </summary>
    /// <remarks>
    /// These tests validate distributed concurrency admission behavior using the Redis ZSET lease model.
    /// The most important invariant is crash-safe capacity recovery: if a worker acquires a lease and
    /// crashes before releasing it, the lease must expire and capacity must become available again.
    /// </remarks>
    public sealed class RedisAiConcurrencyGateIntegrationTests : IAsyncLifetime
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;

        public RedisAiConcurrencyGateIntegrationTests()
        {
            var connectionString =
                Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                ?? "localhost:6379";

            _redis = ConnectionMultiplexer.Connect(connectionString);
            _database = _redis.GetDatabase();
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            await CleanupConcurrencyKeysAsync();
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            await CleanupConcurrencyKeysAsync();

            await _redis.CloseAsync();
            _redis.Dispose();
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Allow_When_No_Limit_Is_Reached()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var context = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var decision = await gate.TryAcquireAsync(context, definition);

            Assert.True(decision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Deny_When_Global_Limit_Is_Reached()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondContext, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
        }

        [Fact]
        public async Task ReleaseAsync_Should_Free_Capacity_For_Next_Acquire()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            Assert.True(firstDecision.Allowed);

            var blockedDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.False(blockedDecision.Allowed);

            await gate.ReleaseAsync(firstContext, definition);

            var recoveredDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.True(recoveredDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Recover_Capacity_When_Lease_Expires()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 1,
                DefaultRetryAfterMs = 50
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            Assert.True(firstDecision.Allowed);

            var blockedDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.False(blockedDecision.Allowed);

            await Task.Delay(TimeSpan.FromMilliseconds(1200));

            var recoveredDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.True(recoveredDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Be_Idempotent_For_Same_Lease_Id()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var context = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "same-lease");

            var firstDecision = await gate.TryAcquireAsync(context, definition);
            var secondDecision = await gate.TryAcquireAsync(context, definition);

            Assert.True(firstDecision.Allowed);
            Assert.True(secondDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Respect_Pipeline_Scope()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxPipelineConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                pipelineKey: "pipeline-a:v1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                pipelineKey: "pipeline-a:v1",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var thirdContextDifferentPipeline = CreateContext(
                executionId: "exec-3",
                pipelineKey: "pipeline-b:v1",
                stepName: "step-c",
                workerId: "worker-3",
                leaseId: "lease-3");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondContext, definition);
            var thirdDecision = await gate.TryAcquireAsync(thirdContextDifferentPipeline, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
            Assert.True(thirdDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Respect_PipelineStep_Scope()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxStepConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                pipelineKey: "pipeline-a:v1",
                stepName: "llm.summary",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondSamePipelineSameStep = CreateContext(
                executionId: "exec-2",
                pipelineKey: "pipeline-a:v1",
                stepName: "llm.summary",
                workerId: "worker-2",
                leaseId: "lease-2");

            var thirdSamePipelineDifferentStep = CreateContext(
                executionId: "exec-3",
                pipelineKey: "pipeline-a:v1",
                stepName: "rag.retrieve",
                workerId: "worker-3",
                leaseId: "lease-3");

            var fourthDifferentPipelineSameStep = CreateContext(
                executionId: "exec-4",
                pipelineKey: "pipeline-b:v1",
                stepName: "llm.summary",
                workerId: "worker-4",
                leaseId: "lease-4");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondSamePipelineSameStep, definition);
            var thirdDecision = await gate.TryAcquireAsync(thirdSamePipelineDifferentStep, definition);
            var fourthDecision = await gate.TryAcquireAsync(fourthDifferentPipelineSameStep, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
            Assert.True(thirdDecision.Allowed);
            Assert.True(fourthDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Respect_Execution_Scope()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxExecutionConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondSameExecution = CreateContext(
                executionId: "exec-1",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var thirdDifferentExecution = CreateContext(
                executionId: "exec-2",
                stepName: "step-c",
                workerId: "worker-3",
                leaseId: "lease-3");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondSameExecution, definition);
            var thirdDecision = await gate.TryAcquireAsync(thirdDifferentExecution, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
            Assert.True(thirdDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Return_Diagnostic_Reason_When_Global_Limit_Is_Reached()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondContext, definition);

            Assert.True(firstDecision.Allowed);

            Assert.False(secondDecision.Allowed);
            Assert.NotNull(secondDecision.Reason);
            Assert.Contains("Concurrency limit reached", secondDecision.Reason);
            Assert.Contains("ai:concurrency:scope:global", secondDecision.Reason);
            Assert.Contains("Current='1'", secondDecision.Reason);
            Assert.Contains("Limit='1'", secondDecision.Reason);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Return_Diagnostic_Reason_When_PipelineStep_Limit_Is_Reached()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxStepConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                pipelineKey: "pipeline-a:v1",
                stepName: "llm.summary",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                pipelineKey: "pipeline-a:v1",
                stepName: "llm.summary",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondContext, definition);

            Assert.True(firstDecision.Allowed);

            Assert.False(secondDecision.Allowed);
            Assert.NotNull(secondDecision.Reason);
            Assert.Contains("Concurrency limit reached", secondDecision.Reason);
            Assert.Contains(
                "ai:concurrency:scope:pipeline-step:pipeline-a:v1:llm.summary",
                secondDecision.Reason);
            Assert.Contains("Current='1'", secondDecision.Reason);
            Assert.Contains("Limit='1'", secondDecision.Reason);
        }

        /// <summary>
        /// Verifies that the claim service does not attempt to claim a ready DAG step
        /// when the distributed concurrency gate denies admission.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        /// <remarks>
        /// This test validates the throttling path:
        /// the DAG store returns a ready step, the concurrency gate denies capacity,
        /// and the claim service must return <c>null</c> without attempting a Redis step claim
        /// and without releasing a lease that was never acquired.
        /// </remarks>
        [Fact]
        public async Task ClaimNextAsync_Should_Not_Claim_When_Concurrency_Gate_Denies()
        {
            // Arrange
            var executionId = "exec-throttled";
            var pipelineKey = "test-pipeline:v1";
            var workerId = "worker-1";
            var stepName = "step-a";

            var dagStore = Substitute.For<IAiDagExecutionStore>();
            var concurrencyGate = Substitute.For<IAiConcurrencyGate>();

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Steps =
        {
            [stepName] = new AiStepState
            {
                StepName = stepName,
                Status = AiStepExecutionStatus.Ready
            }
        }
            };

            dagStore.RecoverTimedOutStepsAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(0);

            dagStore.GetStateAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(state);

            dagStore.GetReadyStepsAsync(
                    executionId,
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(new[]
                {
            new AiClaimedStep
            {
                ExecutionId = executionId,
                StepName = stepName,
                ClaimToken = "ready-token"
            }
                });

            concurrencyGate.TryAcquireAsync(
                    Arg.Any<AiConcurrencyContext>(),
                    Arg.Any<AiConcurrencyDefinition>(),
                    Arg.Any<CancellationToken>())
                .Returns(AiConcurrencyDecision.Deny(
                    "Concurrency limit reached for scope 'ai:concurrency:scope:global'. Current='1', Limit='1'.",
                    TimeSpan.FromMilliseconds(100)));

            var services = CreateEngineServices(
                dagStore: dagStore,
                concurrencyGate: concurrencyGate);

            var service = new AiDagStepClaimService(services);

            // Act
            var claimed = await service.ClaimNextAsync(
                executionId,
                pipelineKey,
                workerId);

            // Assert
            Assert.Null(claimed);

            _ = dagStore.DidNotReceive().TryClaimStepAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

            _ = concurrencyGate.DidNotReceive().ReleaseAsync(
                Arg.Any<AiConcurrencyContext>(),
                Arg.Any<AiConcurrencyDefinition>(),
                Arg.Any<CancellationToken>());
        }

        

        /// <summary>
        /// Verifies that the claim service releases distributed concurrency capacity
        /// when admission succeeds but the DAG step claim fails.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        /// <remarks>
        /// This test validates the distributed claim race path:
        /// a ready step is admitted by the concurrency gate, but another worker may claim
        /// the step first before this worker reaches <c>TryClaimStepAsync</c>.
        /// In that case, the claim service must release the previously acquired
        /// concurrency lease immediately to avoid leaking distributed capacity.
        /// </remarks>
        [Fact]
        public async Task ClaimNextAsync_Should_Release_Concurrency_Lease_When_Dag_Claim_Fails()
        {
            // Arrange
            var executionId = "exec-claim-race";
            var pipelineKey = "test-pipeline:v1";
            var workerId = "worker-1";
            var stepName = "step-a";

            var dagStore = Substitute.For<IAiDagExecutionStore>();
            var concurrencyGate = Substitute.For<IAiConcurrencyGate>();

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Steps =
        {
            [stepName] = new AiStepState
            {
                StepName = stepName,
                Status = AiStepExecutionStatus.Ready
            }
        }
            };

            dagStore.RecoverTimedOutStepsAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(0);

            dagStore.GetStateAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(state);

            dagStore.GetReadyStepsAsync(
                    executionId,
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(new[]
                {
            new AiClaimedStep
            {
                ExecutionId = executionId,
                StepName = stepName,
                ClaimToken = "ready-token"
            }
                });

            concurrencyGate.TryAcquireAsync(
                    Arg.Any<AiConcurrencyContext>(),
                    Arg.Any<AiConcurrencyDefinition>(),
                    Arg.Any<CancellationToken>())
                .Returns(AiConcurrencyDecision.Allow());

            dagStore.TryClaimStepAsync(
                    executionId,
                    stepName,
                    workerId,
                    Arg.Any<CancellationToken>())
                .Returns((AiClaimedStep?)null);

            var services = CreateEngineServices(
                dagStore: dagStore,
                concurrencyGate: concurrencyGate);

            var service = new AiDagStepClaimService(services);

            // Act
            var claimed = await service.ClaimNextAsync(
                executionId,
                pipelineKey,
                workerId);

            // Assert
            Assert.Null(claimed);

            await concurrencyGate.Received(1).TryAcquireAsync(
                Arg.Is<AiConcurrencyContext>(context =>
                    context.ExecutionId == executionId &&
                    context.PipelineKey == pipelineKey &&
                    context.StepId == stepName &&
                    context.StepKey == stepName &&
                    context.RuntimeInstanceId == workerId &&
                    context.LeaseId == $"{executionId}:{stepName}:{workerId}"),
                Arg.Any<AiConcurrencyDefinition>(),
                Arg.Any<CancellationToken>());

            await dagStore.Received(1).TryClaimStepAsync(
                executionId,
                stepName,
                workerId,
                Arg.Any<CancellationToken>());

            await concurrencyGate.Received(1).ReleaseAsync(
                Arg.Is<AiConcurrencyContext>(context =>
                    context.ExecutionId == executionId &&
                    context.PipelineKey == pipelineKey &&
                    context.StepId == stepName &&
                    context.StepKey == stepName &&
                    context.RuntimeInstanceId == workerId &&
                    context.LeaseId == $"{executionId}:{stepName}:{workerId}"),
                Arg.Any<AiConcurrencyDefinition>(),
                Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that batch claiming does not attempt to claim a ready DAG step
        /// when the distributed concurrency gate denies admission.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        /// <remarks>
        /// This test validates the throttled batch path:
        /// the DAG store exposes a ready step, the concurrency gate denies capacity,
        /// and the claim service must return an empty batch without attempting a Redis step claim
        /// and without releasing a lease that was never acquired.
        /// </remarks>
        [Fact]
        public async Task ClaimBatchAsync_Should_Not_Claim_When_Concurrency_Gate_Denies()
        {
            // Arrange
            var executionId = "exec-batch-throttled";
            var pipelineKey = "test-pipeline:v1";
            var workerId = "worker-1";
            var stepName = "step-a";

            var dagStore = Substitute.For<IAiDagExecutionStore>();
            var concurrencyGate = Substitute.For<IAiConcurrencyGate>();

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Steps =
        {
            [stepName] = new AiStepState
            {
                StepName = stepName,
                Status = AiStepExecutionStatus.Ready
            }
        }
            };

            dagStore.RecoverTimedOutStepsAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(0);

            dagStore.GetStateAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(state);

            dagStore.GetReadyStepsAsync(
                    executionId,
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(new[]
                {
            new AiClaimedStep
            {
                ExecutionId = executionId,
                StepName = stepName,
                ClaimToken = "ready-token"
            }
                });

            concurrencyGate.TryAcquireAsync(
                    Arg.Any<AiConcurrencyContext>(),
                    Arg.Any<AiConcurrencyDefinition>(),
                    Arg.Any<CancellationToken>())
                .Returns(AiConcurrencyDecision.Deny(
                    "Concurrency limit reached for scope 'ai:concurrency:scope:global'. Current='1', Limit='1'.",
                    TimeSpan.FromMilliseconds(100)));

            var services = CreateEngineServices(
                dagStore: dagStore,
                concurrencyGate: concurrencyGate);

            var service = new AiDagStepClaimService(services);

            // Act
            var claimedSteps = await service.ClaimBatchAsync(
                executionId,
                pipelineKey,
                workerId,
                maxSteps: 4);

            // Assert
            Assert.Empty(claimedSteps);

            _ = dagStore.DidNotReceive().TryClaimStepAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

            _ = concurrencyGate.DidNotReceive().ReleaseAsync(
                Arg.Any<AiConcurrencyContext>(),
                Arg.Any<AiConcurrencyDefinition>(),
                Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Verifies that batch claiming releases distributed concurrency capacity
        /// when admission succeeds but the DAG step claim fails.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous test operation.
        /// </returns>
        /// <remarks>
        /// This test validates the distributed batch claim race path:
        /// a ready step is admitted by the concurrency gate, but another worker may claim
        /// the step first before this worker reaches <c>TryClaimStepAsync</c>.
        /// The service must release the acquired concurrency lease immediately and exclude
        /// the step from the returned batch.
        /// </remarks>
        [Fact]
        public async Task ClaimBatchAsync_Should_Release_Concurrency_Lease_When_Dag_Claim_Fails()
        {
            // Arrange
            var executionId = "exec-batch-claim-race";
            var pipelineKey = "test-pipeline:v1";
            var workerId = "worker-1";
            var stepName = "step-a";

            var dagStore = Substitute.For<IAiDagExecutionStore>();
            var concurrencyGate = Substitute.For<IAiConcurrencyGate>();

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Steps =
        {
            [stepName] = new AiStepState
            {
                StepName = stepName,
                Status = AiStepExecutionStatus.Ready
            }
        }
            };

            dagStore.RecoverTimedOutStepsAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(0);

            dagStore.GetStateAsync(
                    executionId,
                    Arg.Any<CancellationToken>())
                .Returns(state);

            dagStore.GetReadyStepsAsync(
                    executionId,
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(new[]
                {
            new AiClaimedStep
            {
                ExecutionId = executionId,
                StepName = stepName,
                ClaimToken = "ready-token"
            }
                });

            concurrencyGate.TryAcquireAsync(
                    Arg.Any<AiConcurrencyContext>(),
                    Arg.Any<AiConcurrencyDefinition>(),
                    Arg.Any<CancellationToken>())
                .Returns(AiConcurrencyDecision.Allow());

            dagStore.TryClaimStepAsync(
                    executionId,
                    stepName,
                    workerId,
                    Arg.Any<CancellationToken>())
                .Returns((AiClaimedStep?)null);

            var services = CreateEngineServices(
                dagStore: dagStore,
                concurrencyGate: concurrencyGate);

            var service = new AiDagStepClaimService(services);

            // Act
            var claimedSteps = await service.ClaimBatchAsync(
                executionId,
                pipelineKey,
                workerId,
                maxSteps: 4);

            // Assert
            Assert.Empty(claimedSteps);

            _ = concurrencyGate.Received(1).TryAcquireAsync(
                Arg.Is<AiConcurrencyContext>(context =>
                    context.ExecutionId == executionId &&
                    context.PipelineKey == pipelineKey &&
                    context.StepId == stepName &&
                    context.StepKey == stepName &&
                    context.RuntimeInstanceId == workerId &&
                    context.LeaseId == $"{executionId}:{stepName}:{workerId}"),
                Arg.Any<AiConcurrencyDefinition>(),
                Arg.Any<CancellationToken>());

            _ = dagStore.Received(1).TryClaimStepAsync(
                executionId,
                stepName,
                workerId,
                Arg.Any<CancellationToken>());

            _ = concurrencyGate.Received(1).ReleaseAsync(
                Arg.Is<AiConcurrencyContext>(context =>
                    context.ExecutionId == executionId &&
                    context.PipelineKey == pipelineKey &&
                    context.StepId == stepName &&
                    context.StepKey == stepName &&
                    context.RuntimeInstanceId == workerId &&
                    context.LeaseId == $"{executionId}:{stepName}:{workerId}"),
                Arg.Any<AiConcurrencyDefinition>(),
                Arg.Any<CancellationToken>());
        }

        private static IAiDagExecutionEngineServices CreateEngineServices(
            IAiDagExecutionStore dagStore,
            IAiConcurrencyGate concurrencyGate)
        {
            var services = Substitute.For<IAiDagExecutionEngineServices>();

            var logger = Substitute.For<IAiRuntimeLogger>();
            var observability = Substitute.For<IAiRuntimeObservability>();

            services.DagStore.Returns(dagStore);
            services.ConcurrencyGate.Returns(concurrencyGate);
            services.Logger.Returns(logger);
            services.ObservabilityService.Returns(observability);

            return services;
        }

        private static AiConcurrencyContext CreateContext(
            string executionId,
            string stepName,
            string workerId,
            string leaseId,
            string pipelineKey = "pipeline-a:v1")
        {
            return new AiConcurrencyContext
            {
                ExecutionId = executionId,
                PipelineKey = pipelineKey,
                StepId = stepName,
                StepKey = stepName,
                RuntimeInstanceId = workerId,
                LeaseId = leaseId
            };
        }

        private async Task CleanupConcurrencyKeysAsync()
        {
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);

                if (!server.IsConnected)
                {
                    continue;
                }

                var keys = server.Keys(pattern: "ai:concurrency:scope:*").ToArray();

                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }
            }
        }
    }
}