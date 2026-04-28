using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Retention
{
    /// <summary>
    /// Production-like integration tests for Hybrid retention.
    ///
    /// PURPOSE:
    /// - Validate Hybrid retention with the real DAG engine.
    /// - Validate real RetentionService orchestration.
    /// - Validate real archive index + step payload store integration through the fixture.
    /// - Validate resolver visibility after eviction.
    ///
    /// IMPORTANT:
    /// - This is intentionally small and fast.
    /// - This is NOT a stress test.
    /// - Large graphs and multi-worker chaos belong in separate stress suites.
    /// </summary>
    public sealed class AiDagHybridRetentionProductionIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";

        /// <summary>
        /// Verifies that a real DAG execution can complete with Hybrid retention enabled,
        /// and that at least one archived step remains resolvable after eviction.
        ///
        /// SCENARIO:
        /// - A real DAG pipeline is executed through AiDagExecutionEngine.
        /// - Hybrid retention is enabled with a small MaxCompletedStepsInState.
        /// - Completed steps should be compacted and overflow steps should be evicted.
        /// - Evicted steps should be saved into the step payload store and indexed.
        ///
        /// EXPECTATION:
        /// - Execution reaches a terminal state.
        /// - Hot state is bounded by retention.
        /// - Archive index contains evicted step metadata.
        /// - Resolver can return status without loading the full payload.
        /// - Resolver can load the full archived step on demand.
        ///
        /// WHY THIS MATTERS:
        /// - This validates the production retention path end-to-end without using a slow stress scenario.
        /// - It protects against regressions where Hybrid retention completes but archived steps become invisible.
        /// </summary>
        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_With_Hybrid_Retention_And_Archived_Steps_Resolvable()
        {
            var options = CreateOptions();

            options.PayloadStore.Provider = "mongo-redis";
            options.StateRetention.MaxCompletedStepsInState = 3;
            options.RetentionTrigger.MaxCompletedStepsInState = options.StateRetention.MaxCompletedStepsInState;
            options.RetentionTrigger.MaxStepsInState = options.StateRetention.MaxCompletedStepsInState;
            options.RetentionTrigger.MaxInlinePayloadBytes = 1;

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "dag-complex",
                "hello");

            var finalRecord = await host.Engine.ExecuteAllAsync(
                created.ExecutionId);

            Assert.NotNull(finalRecord);
            Assert.True(
                finalRecord.IsTerminal,
                $"Execution should be terminal. Status={finalRecord.Status}");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();
            var payloadStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadStore>();

            var finalState = await dagStore.GetStateAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(finalState);
            Assert.NotEmpty(finalState!.Steps);

            Assert.True(
                finalState.Steps.Count <= options.StateRetention.MaxCompletedStepsInState,
                $"Hot state should be bounded. Actual={finalState.Steps.Count}, Max={options.StateRetention.MaxCompletedStepsInState}");

            var archivedEntries = await indexStore.GetByExecutionAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(archivedEntries);
            Assert.NotEmpty(archivedEntries);

            var archivedEntry = archivedEntries
                .OrderBy(x => x.ArchivedAtUtc)
                .First();

            Assert.False(
                finalState.Steps.ContainsKey(archivedEntry.StepName),
                "Archived step should not still exist in hot state.");

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore);

            var statusOnlyStep = await resolver.GetStepStatusAsync(
                created.ExecutionId,
                archivedEntry.StepName,
                finalState,
                CancellationToken.None);

            Assert.NotNull(statusOnlyStep);
            Assert.Equal(archivedEntry.StepName, statusOnlyStep!.StepName);
            Assert.Equal(archivedEntry.Status, statusOnlyStep.Status);

            var fullArchivedStep = await resolver.GetStepAsync(
                created.ExecutionId,
                archivedEntry.StepName,
                finalState,
                CancellationToken.None);

            Assert.NotNull(fullArchivedStep);
            Assert.Equal(archivedEntry.StepName, fullArchivedStep!.StepName);
            Assert.Equal(archivedEntry.Status, fullArchivedStep.Status);
        }

        /// <summary>
        /// Verifies that a second ExecuteAllAsync call after terminal completion does not break
        /// archived-step visibility or retention state.
        ///
        /// EXPECTATION:
        /// - Re-running ExecuteAllAsync on a terminal execution is safe/idempotent.
        /// - Archive metadata remains available.
        /// - Hot state remains bounded.
        ///
        /// WHY THIS MATTERS:
        /// - Real systems often re-read or re-drive terminal executions.
        /// - Retention must not create a state where terminal re-entry loops or loses archive visibility.
        /// </summary>
        [Fact]
        public async Task ExecuteAllAsync_Should_Remain_Idempotent_After_Hybrid_Retention()
        {
            var options = CreateOptions();
            options.RetentionTrigger.MaxCompletedStepsInState = options.StateRetention.MaxCompletedStepsInState;
            options.RetentionTrigger.MaxStepsInState = options.StateRetention.MaxCompletedStepsInState;
            options.RetentionTrigger.MaxInlinePayloadBytes = 1;

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var created = await host.Engine.CreateAsync(
                "dag-complex",
                "hello");

            var first = await host.Engine.ExecuteAllAsync(
                created.ExecutionId);

            Assert.True(first.IsTerminal);

            var second = await host.Engine.ExecuteAllAsync(
                created.ExecutionId);

            Assert.True(second.IsTerminal);
            Assert.Equal(first.ExecutionId, second.ExecutionId);
            Assert.Equal(first.Status, second.Status);

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();

            var state = await dagStore.GetStateAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(state);
            Assert.NotEmpty(state!.Steps);

            Assert.True(
                state.Steps.Count <= options.StateRetention.MaxCompletedStepsInState,
                $"Hot state should remain bounded. Actual={state.Steps.Count}, Max={options.StateRetention.MaxCompletedStepsInState}");

            var archivedEntries = await indexStore.GetByExecutionAsync(
                created.ExecutionId,
                CancellationToken.None);

            Assert.NotNull(archivedEntries);
            Assert.NotEmpty(archivedEntries);
        }

        

        /// <summary>
        /// Creates production-like test options for fast Hybrid retention validation.
        ///
        /// IMPORTANT:
        /// - Uses a small existing DAG pipeline.
        /// - Uses a low retention threshold to force Hybrid behavior.
        /// - PayloadStore defaults are centralized in AiDagExecutionEngineFixture.
        /// - Cleanup is disabled so assertions can inspect final state and archive index.
        /// </summary>
        private static AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\dag-complex-10-steps.json",

                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Hybrid,
                    MaxCompletedStepsInState = 5
                },

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
