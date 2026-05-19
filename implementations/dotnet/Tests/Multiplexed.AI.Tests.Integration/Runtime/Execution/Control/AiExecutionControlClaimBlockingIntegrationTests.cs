using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Control;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Control
{
    /// <summary>
    /// Integration tests proving that execution control state blocks distributed step claims.
    /// </summary>
    /// <remarks>
    /// These tests validate the runtime integration point where <see cref="IAiExecutionControlGate"/>
    /// is evaluated before DAG step claiming. They intentionally verify control behavior through
    /// the public DAG execution engine instead of calling the claim service directly.
    /// </remarks>
    [Collection("redis")]
    public sealed class AiExecutionControlClaimBlockingIntegrationTests
    {
        private readonly IConnectionMultiplexer _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionControlClaimBlockingIntegrationTests"/> class.
        /// </summary>
        /// <param name="fixture">The Redis integration test fixture.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fixture"/> is null.
        /// </exception>
        public AiExecutionControlClaimBlockingIntegrationTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);

            _connection = fixture.Connection;
        }

        /// <summary>
        /// Verifies that a paused execution does not claim or execute a ready DAG step.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_WhenExecutionIsPausing_ShouldNotClaimReadyStep()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: "mongodb://localhost:27017",
                mongoDatabaseName: "multiplexed_ai_tests",
                redisConnectionString: "localhost:6379");

            var created = await host.Engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                var controlService = host.ServiceProvider.GetRequiredService<IAiExecutionControlService>();

                await controlService.PauseExecutionAsync(
                        created.ExecutionId,
                        reason: "test pause",
                        requestedBy: "integration-test")
                    .ConfigureAwait(false);

                var record = await host.Engine.ExecuteNextAsync(created.ExecutionId)
                    .ConfigureAwait(false);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
                var state = await dagStore.GetStateAsync(created.ExecutionId).ConfigureAwait(false);

                Assert.NotNull(record);
                Assert.Empty(record.CompletedSteps);
                Assert.NotNull(state);

                AssertNoStepWasClaimedOrCompleted(state!);
            }
            finally
            {
                await CleanupExecutionAsync(created.ExecutionId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Verifies that an execution waiting for human input does not claim or execute a ready DAG step.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_WhenExecutionIsWaitingForInput_ShouldNotClaimReadyStep()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: "mongodb://localhost:27017",
                mongoDatabaseName: "multiplexed_ai_tests",
                redisConnectionString: "localhost:6379");

            var created = await host.Engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                var controlService = host.ServiceProvider.GetRequiredService<IAiExecutionControlService>();

                await controlService.MarkWaitingForInputAsync(
                        created.ExecutionId,
                        waitingKey: "approval:test",
                        waitingStepName: "start",
                        reason: "test human input",
                        requestedBy: "integration-test")
                    .ConfigureAwait(false);

                var record = await host.Engine.ExecuteNextAsync(created.ExecutionId)
                    .ConfigureAwait(false);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
                var state = await dagStore.GetStateAsync(created.ExecutionId).ConfigureAwait(false);

                Assert.NotNull(record);
                Assert.Empty(record.CompletedSteps);
                Assert.NotNull(state);

                AssertNoStepWasClaimedOrCompleted(state!);
            }
            finally
            {
                await CleanupExecutionAsync(created.ExecutionId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Verifies that a cancelling execution does not claim or execute a ready DAG step.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_WhenExecutionIsCancelling_ShouldNotClaimReadyStep()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: "mongodb://localhost:27017",
                mongoDatabaseName: "multiplexed_ai_tests",
                redisConnectionString: "localhost:6379");

            var created = await host.Engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                var controlService = host.ServiceProvider.GetRequiredService<IAiExecutionControlService>();

                await controlService.CancelExecutionAsync(
                        created.ExecutionId,
                        reason: "test cancel",
                        requestedBy: "integration-test")
                    .ConfigureAwait(false);

                var record = await host.Engine.ExecuteNextAsync(created.ExecutionId)
                    .ConfigureAwait(false);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
                var state = await dagStore.GetStateAsync(created.ExecutionId).ConfigureAwait(false);

                Assert.NotNull(record);
                Assert.Empty(record.CompletedSteps);
                Assert.NotNull(state);

                AssertNoStepWasClaimedOrCompleted(state!);
            }
            finally
            {
                await CleanupExecutionAsync(created.ExecutionId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Verifies that an execution without control state still claims and executes normally.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_WhenNoControlStateExists_ShouldClaimAndExecuteReadyStep()
        {
            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                mongoConnectionString: "mongodb://localhost:27017",
                mongoDatabaseName: "multiplexed_ai_tests",
                redisConnectionString: "localhost:6379");

            var created = await host.Engine.CreateAsync("dag-parallel-basic", "Marco");

            try
            {
                var record = await host.Engine.ExecuteNextAsync(created.ExecutionId)
                    .ConfigureAwait(false);

                var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
                var state = await dagStore.GetStateAsync(created.ExecutionId).ConfigureAwait(false);

                Assert.NotNull(record);
                Assert.Contains("start", record.CompletedSteps);
                Assert.NotNull(state);
                Assert.Equal(AiStepExecutionStatus.Completed, state!.Steps["start"].Status);
            }
            finally
            {
                await CleanupExecutionAsync(created.ExecutionId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates runtime options for the basic Redis DAG pipeline scenario.
        /// </summary>
        /// <returns>The configured AI engine options.</returns>
        private static AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "Json",
                JsonPipelineDefinitionFilePath = "config/dag-parallel-basic.json",
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

        /// <summary>
        /// Asserts that no step entered running or completed state.
        /// </summary>
        /// <param name="state">The DAG execution state.</param>
        private static void AssertNoStepWasClaimedOrCompleted(AiExecutionState state)
        {
            Assert.DoesNotContain(
                state.Steps.Values,
                step => step.Status == AiStepExecutionStatus.Running);

            Assert.DoesNotContain(
                state.Steps.Values,
                step => step.Status == AiStepExecutionStatus.Completed);
        }

        /// <summary>
        /// Performs best-effort cleanup of Redis execution and control keys.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        private async Task CleanupExecutionAsync(string executionId)
        {
            var db = _connection.GetDatabase();

            var recordKey = $"ai:execution:record:{executionId}";
            var stateKey = $"ai:execution:state:{executionId}";
            var stepsIndexKey = $"ai:execution:steps:{executionId}";
            var controlKey = $"ai:execution:control:{executionId}";

            var stepNames = await db.SetMembersAsync(stepsIndexKey).ConfigureAwait(false);

            foreach (var stepName in stepNames)
            {
                if (stepName.IsNullOrEmpty)
                {
                    continue;
                }

                await db.KeyDeleteAsync($"ai:execution:step:{executionId}:{stepName}")
                    .ConfigureAwait(false);
            }

            await db.KeyDeleteAsync(recordKey).ConfigureAwait(false);
            await db.KeyDeleteAsync(stateKey).ConfigureAwait(false);
            await db.KeyDeleteAsync(stepsIndexKey).ConfigureAwait(false);
            await db.KeyDeleteAsync(controlKey).ConfigureAwait(false);
        }
    }
}