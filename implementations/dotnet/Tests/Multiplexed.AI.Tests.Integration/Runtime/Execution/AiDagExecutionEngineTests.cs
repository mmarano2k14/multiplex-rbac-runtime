using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Basic end-to-end DAG execution integration tests.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate DAG execution using the production-style fixture.
    /// - Validate step completion persistence.
    /// - Validate state transitions through the real DAG engine graph.
    ///
    /// RETENTION MIGRATION:
    /// - Legacy retention services/options/resolvers are no longer used.
    /// - Retention is policy-driven and config-driven.
    /// - Tests without retention config execute with retention as no-op.
    ///
    /// IMPORTANT:
    /// - DAG execution state must be read from <see cref="IAiDagExecutionStore"/>.
    /// - <see cref="IAiExecutionStore"/> may not contain the distributed DAG state when
    ///   the fixture uses the Redis/Lua DAG store path.
    /// </remarks>
    public sealed class AiDagExecutionEngineTests
    {
        /// <summary>
        /// Verifies that ExecuteAll completes a basic DAG pipeline.
        /// </summary>
        [Fact]
        public async Task ExecuteAllAsync_Should_Complete_Basic_Dag_Pipeline()
        {
            await using var host = await CreateHostAsync();

            var accessor =
                host.ServiceProvider.GetRequiredService<IExecutionContextAccessor>();

            if (accessor is ExecutionContextAccessor executionAccessor)
            {
                executionAccessor.Set(CreateRuntimeContext());
            }

            var created = await host.Engine.CreateAsync(
                "dag-parallel-basic",
                "Marco");

            var finalRecord = await host.Engine.ExecuteAllAsync(
                created.ExecutionId);

            Assert.NotNull(finalRecord);

            Assert.Equal(
                AiExecutionMode.Dag,
                finalRecord.ExecutionMode);

            Assert.Equal(
                AiExecutionStatus.Completed,
                finalRecord.Status);

            Assert.Equal(
                4,
                finalRecord.CompletedSteps.Count);

            Assert.Contains("start", finalRecord.CompletedSteps);
            Assert.Contains("a1", finalRecord.CompletedSteps);
            Assert.Contains("a2", finalRecord.CompletedSteps);
            Assert.Contains("merge", finalRecord.CompletedSteps);

            var dagStore =
                host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

            var stateWriter =
                host.ServiceProvider.GetRequiredService<IAiExecutionStateWriter>();

            var state = await dagStore.GetStateAsync(
                finalRecord.ExecutionId);

            Assert.NotNull(state);

            Assert.Equal(
                AiStepExecutionStatus.Completed,
                stateWriter.GetOrCreateStep(state!, "start").Status);

            Assert.Equal(
                AiStepExecutionStatus.Completed,
                stateWriter.GetOrCreateStep(state, "a1").Status);

            Assert.Equal(
                AiStepExecutionStatus.Completed,
                stateWriter.GetOrCreateStep(state, "a2").Status);

            Assert.Equal(
                AiStepExecutionStatus.Completed,
                stateWriter.GetOrCreateStep(state, "merge").Status);
        }

        /// <summary>
        /// Creates a production-style DAG execution test host.
        /// </summary>
        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync()
        {
            var options = new AiEngineOptions
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

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: "mongodb://localhost:27017",
                mongoDatabaseName: "multiplexed_ai_tests");
        }

        /// <summary>
        /// Creates a deterministic runtime context for integration tests.
        /// </summary>
        private static ExecutionContext CreateRuntimeContext()
        {
            return new ExecutionContext
            {
                ContextKey = string.Empty,
                Project = "Project",
                TenantId = "tenant-id-xxxx",
                TenantGroupId = "tenant-group-id-xxx",
                CurrentNamespace = "Namespace",
                UserId = "userId",
                Namespaces = new List<NamespaceEntry>
                {
                    new NamespaceEntry
                    {
                        Name = "Namespace",
                        Trns = new HashSet<string>
                        {
                            "trn:Project:crm:billing:invoice:read",
                            "trn:Project:crm:billing:invoice:refund"
                        }
                    }
                },
                TtlSeconds = 300
            };
        }
    }
}
