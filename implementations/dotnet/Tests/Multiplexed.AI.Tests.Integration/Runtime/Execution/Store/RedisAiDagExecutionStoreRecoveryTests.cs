using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Store
{
    public sealed class RedisAiDagExecutionStoreRecoveryTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";

        /// <summary>
        /// Verifies that Redis DAG recovery resets a timed-out Running step
        /// without consuming retry budget.
        ///
        /// SCENARIO:
        /// - A step is created as Ready.
        /// - The store claims it using TryClaimNextReadyStepAsync.
        /// - The claimed step becomes Running.
        /// - Recovery is executed.
        ///
        /// EXPECTATION:
        /// - The Running step is recovered back to Ready.
        /// - RecoveryCount increments.
        /// - RetryCount remains unchanged.
        ///
        /// WHY THIS MATTERS:
        /// - Recovery repairs abandoned infrastructure claims.
        /// - Recovery must not consume business retry budget.
        /// </summary>
        [RedisFact]
        public async Task RecoverTimedOutStepsAsync_Should_Reset_Running_Step_Without_Incrementing_RetryCount()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var store = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Status = AiExecutionStatus.Running,
                ExecutionStepKey = Guid.NewGuid().ToString("N"),
                CompletedSteps = new List<string>()
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = new AiStepState
                    {
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Ready,
                        RetryCount = 0,
                        MaxRetries = 3,
                        RecoveryCount = 0,
                        ClaimTimeoutSeconds = 1
                    }
                }
            };

            await store.CreateAsync(record, state, CancellationToken.None);

            var claimed = await store.TryClaimNextReadyStepAsync(
                executionId,
                "worker-1",
                CancellationToken.None);

            Assert.NotNull(claimed);
            Assert.Equal("step-1", claimed!.StepName);

            var stateAfterClaim = await store.GetStateAsync(executionId, CancellationToken.None);
            Assert.NotNull(stateAfterClaim);

            var runningStep = stateAfterClaim!.Steps["step-1"];

            Assert.Equal(AiStepExecutionStatus.Running, runningStep.Status);
            Assert.Equal(0, runningStep.RetryCount);
            Assert.Equal(0, runningStep.RecoveryCount);
            Assert.False(string.IsNullOrWhiteSpace(runningStep.ClaimToken));
            Assert.False(string.IsNullOrWhiteSpace(runningStep.ClaimedBy));
            Assert.True(runningStep.ClaimedAtUtc.HasValue);

            // Wait until the claim timeout is definitely expired.
            // ClaimTimeoutSeconds = 1, so 1200ms avoids timing edge cases.
            await Task.Delay(TimeSpan.FromMilliseconds(1200));


            var recoveredCount = await store.RecoverTimedOutStepsAsync(
                executionId,
                CancellationToken.None);

            Assert.Equal(1, recoveredCount);

            var stateAfterRecovery = await store.GetStateAsync(executionId, CancellationToken.None);
            Assert.NotNull(stateAfterRecovery);

            var recoveredStep = stateAfterRecovery!.Steps["step-1"];

            Assert.Equal(AiStepExecutionStatus.Ready, recoveredStep.Status);
            Assert.Equal(0, recoveredStep.RetryCount);
            Assert.Equal(1, recoveredStep.RecoveryCount);

            Assert.Null(recoveredStep.ClaimToken);
            Assert.Null(recoveredStep.ClaimedBy);
            Assert.Null(recoveredStep.ClaimedAtUtc);
        }

        /// <summary>
        /// Verifies that Redis DAG recovery does not reset a Running step
        /// when the claim timeout has not expired yet.
        ///
        /// SCENARIO:
        /// - A step is created as Ready.
        /// - The store claims it using TryClaimNextReadyStepAsync.
        /// - The claimed step becomes Running.
        /// - Recovery is executed before ClaimTimeoutSeconds expires.
        ///
        /// EXPECTATION:
        /// - No step is recovered.
        /// - The step remains Running.
        /// - RecoveryCount remains unchanged.
        /// - RetryCount remains unchanged.
        /// - Claim metadata remains present.
        ///
        /// WHY THIS MATTERS:
        /// - Recovery must not steal work from a healthy active worker.
        /// - Timeout recovery should only repair abandoned claims.
        /// </summary>
        [RedisFact]
        public async Task RecoverTimedOutStepsAsync_Should_Not_Reset_Running_Step_When_Claim_Has_Not_Expired()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var store = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Status = AiExecutionStatus.Running,
                ExecutionStepKey = Guid.NewGuid().ToString("N"),
                CompletedSteps = new List<string>()
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = new AiStepState
                    {
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Ready,
                        RetryCount = 0,
                        MaxRetries = 3,
                        RecoveryCount = 0,
                        ClaimTimeoutSeconds = 30
                    }
                }
            };

            await store.CreateAsync(record, state, CancellationToken.None);

            var claimed = await store.TryClaimNextReadyStepAsync(
                executionId,
                "worker-1",
                CancellationToken.None);

            Assert.NotNull(claimed);
            Assert.Equal("step-1", claimed!.StepName);

            var recoveredCount = await store.RecoverTimedOutStepsAsync(
                executionId,
                CancellationToken.None);

            Assert.Equal(0, recoveredCount);

            var stateAfterRecovery = await store.GetStateAsync(executionId, CancellationToken.None);
            Assert.NotNull(stateAfterRecovery);

            var step = stateAfterRecovery!.Steps["step-1"];

            Assert.Equal(AiStepExecutionStatus.Running, step.Status);
            Assert.Equal(0, step.RetryCount);
            Assert.Equal(0, step.RecoveryCount);

            Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
            Assert.False(string.IsNullOrWhiteSpace(step.ClaimedBy));
            Assert.True(step.ClaimedAtUtc.HasValue);
        }

        /// <summary>
        /// Verifies that Redis DAG recovery ignores terminal steps.
        ///
        /// SCENARIO:
        /// - One step is Completed.
        /// - One step is Failed.
        /// - Both have old claim-like metadata.
        /// - Recovery is executed.
        ///
        /// EXPECTATION:
        /// - No step is recovered.
        /// - Completed remains Completed.
        /// - Failed remains Failed.
        /// - RetryCount and RecoveryCount remain unchanged.
        ///
        /// WHY THIS MATTERS:
        /// - Recovery must only repair timed-out Running claims.
        /// - Terminal step states are immutable for recovery purposes.
        /// </summary>
        [RedisFact]
        public async Task RecoverTimedOutStepsAsync_Should_Not_Reset_Terminal_Steps()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var store = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Status = AiExecutionStatus.Running,
                ExecutionStepKey = Guid.NewGuid().ToString("N"),
                CompletedSteps = new List<string> { "completed-step" }
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                Steps = new Dictionary<string, AiStepState>
                {
                    ["completed-step"] = new AiStepState
                    {
                        StepName = "completed-step",
                        Status = AiStepExecutionStatus.Completed,
                        RetryCount = 1,
                        MaxRetries = 3,
                        RecoveryCount = 2,
                        ClaimTimeoutSeconds = 1,
                        ClaimedBy = "worker-old",
                        ClaimToken = Guid.NewGuid().ToString("N"),
                        ClaimedAtUtc = DateTime.UtcNow.AddMinutes(-10)
                    },

                    ["failed-step"] = new AiStepState
                    {
                        StepName = "failed-step",
                        Status = AiStepExecutionStatus.Failed,
                        RetryCount = 3,
                        MaxRetries = 3,
                        RecoveryCount = 1,
                        ClaimTimeoutSeconds = 1,
                        ClaimedBy = "worker-old",
                        ClaimToken = Guid.NewGuid().ToString("N"),
                        ClaimedAtUtc = DateTime.UtcNow.AddMinutes(-10)
                    }
                }
            };

            await store.CreateAsync(record, state, CancellationToken.None);

            var recoveredCount = await store.RecoverTimedOutStepsAsync(
                executionId,
                CancellationToken.None);

            Assert.Equal(0, recoveredCount);

            var stateAfterRecovery = await store.GetStateAsync(executionId, CancellationToken.None);
            Assert.NotNull(stateAfterRecovery);

            var completed = stateAfterRecovery!.Steps["completed-step"];
            var failed = stateAfterRecovery.Steps["failed-step"];

            Assert.Equal(AiStepExecutionStatus.Completed, completed.Status);
            Assert.Equal(1, completed.RetryCount);
            Assert.Equal(2, completed.RecoveryCount);

            Assert.Equal(AiStepExecutionStatus.Failed, failed.Status);
            Assert.Equal(3, failed.RetryCount);
            Assert.Equal(1, failed.RecoveryCount);
        }

        /// <summary>
        /// Verifies that repeated recovery attempts do not reset a Running step
        /// while its claim is still valid.
        ///
        /// SCENARIO:
        /// - A step is claimed and becomes Running.
        /// - ClaimTimeoutSeconds is high enough that the claim is still valid.
        /// - Recovery is called multiple times before timeout expiration.
        ///
        /// EXPECTATION:
        /// - No recovery happens.
        /// - The step remains Running.
        /// - RecoveryCount stays unchanged.
        /// - RetryCount stays unchanged.
        /// - Claim metadata remains intact.
        ///
        /// WHY THIS MATTERS:
        /// - Recovery may run periodically in production.
        /// - Repeated recovery scans must not steal healthy in-flight work.
        /// </summary>
        [RedisFact]
        public async Task RecoverTimedOutStepsAsync_Should_Not_Reset_Running_Step_On_Repeated_Recovery_When_Claim_Has_Not_Expired()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var store = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var executionId = Guid.NewGuid().ToString("N");

            var record = new AiExecutionRecord
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Status = AiExecutionStatus.Running,
                ExecutionStepKey = Guid.NewGuid().ToString("N"),
                CompletedSteps = new List<string>()
            };

            var state = new AiExecutionState
            {
                ExecutionId = executionId,
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = new AiStepState
                    {
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Ready,
                        RetryCount = 0,
                        MaxRetries = 3,
                        RecoveryCount = 0,
                        ClaimTimeoutSeconds = 30
                    }
                }
            };

            await store.CreateAsync(record, state, CancellationToken.None);

            var claimed = await store.TryClaimNextReadyStepAsync(
                executionId,
                "worker-1",
                CancellationToken.None);

            Assert.NotNull(claimed);
            Assert.Equal("step-1", claimed!.StepName);

            for (var i = 0; i < 3; i++)
            {
                var recoveredCount = await store.RecoverTimedOutStepsAsync(
                    executionId,
                    CancellationToken.None);

                Assert.Equal(0, recoveredCount);
            }

            var stateAfterRecovery = await store.GetStateAsync(executionId, CancellationToken.None);
            Assert.NotNull(stateAfterRecovery);

            var step = stateAfterRecovery!.Steps["step-1"];

            Assert.Equal(AiStepExecutionStatus.Running, step.Status);
            Assert.Equal(0, step.RetryCount);
            Assert.Equal(0, step.RecoveryCount);

            Assert.Equal("worker-1", step.ClaimedBy);
            Assert.False(string.IsNullOrWhiteSpace(step.ClaimToken));
            Assert.True(step.ClaimedAtUtc.HasValue);
        }

        private static AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\dag-complex-10-steps.json",
                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = false
                },
                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                }
            };
        }
    }
}