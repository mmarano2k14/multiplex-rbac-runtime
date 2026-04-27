using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Pipeline.Registry;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Integration tests for <see cref="AiSequentialExecutionEngine.ExecuteAllAsync"/>.
    ///
    /// PURPOSE:
    /// - Validate full sequential workflow execution.
    /// - Verify all steps are executed in order.
    /// - Verify step results are persisted.
    /// - Verify the final execution record reaches a terminal completed state.
    ///
    /// ARCHITECTURE:
    /// - State mutation is performed through <see cref="IAiExecutionStateWriter"/>.
    /// - State reading is performed through <see cref="IAiExecutionStateReader"/>.
    /// - <see cref="AiExecutionState"/> remains a persistence model only.
    /// </summary>
    public sealed class AiExecutionEngineExecuteAllTests
    {
        /// <summary>
        /// Validates that <see cref="AiSequentialExecutionEngine.ExecuteAllAsync"/> executes
        /// all remaining steps until the workflow reaches a terminal completed state.
        /// </summary>
        [Fact]
        public async Task ExecuteAllAsync_Should_Run_All_Remaining_Steps_And_Complete_Workflow()
        {
            // -----------------------------
            // Arrange
            // -----------------------------

            var step1 = new FakeStep("step-1");
            var step2 = new FakeStep("step-2");

            var store = new FakeInMemoryExecutionStore();
            var contextStore = new FakeInMemoryContextStore();
            var accessor = new FakeInMemoryContextAccessor();
            var factory = new FakeExecutionContextFactory();
            var executor = new FakeStepExecutor();
            var logger = new NoopLogger();

            IAiExecutionStateWriter stateWriter = new DefaultAiExecutionStateWriter();
            IAiExecutionStateReader stateReader = new DefaultAiExecutionStateReader(
                new NoopPayloadResolver());

            var definitionProvider = new InMemoryAiPipelineDefinitionProvider(
                new[]
                {
                    new AiPipelineDefinition
                    {
                        Name = "test-pipeline",
                        Steps = new[]
                        {
                            new AiPipelineStepDefinition
                            {
                                Name = "step-1",
                                StepKey = "step-1",
                                Order = 0
                            },
                            new AiPipelineStepDefinition
                            {
                                Name = "step-2",
                                StepKey = "step-2",
                                Order = 1
                            }
                        }
                    }
                });

            var sourceSelector = new FakeAiPipelineDefinitionSourceSelector(definitionProvider);

            var stepRegistry = new InMemoryAiStepRegistry(
                new[]
                {
                    new KeyValuePair<string, IAiStep>("step-1", step1),
                    new KeyValuePair<string, IAiStep>("step-2", step2)
                });

            var resolver = new AiPipelineResolver(stepRegistry);

            var pipelineExecutor = new AiSequentialPipelineExecutor(
                sourceSelector,
                resolver,
                executor);

            var cleanupService = new NoOpAiExecutionCleanupService();

            var cleanupOptions = Options.Create(new AiExecutionCleanupOptions
            {
                AutoCleanupOnCompleted = false,
                AutoCleanupOnFailed = false,
                SuppressCleanupExceptions = true
            });

            var services = new ServiceCollection().BuildServiceProvider();

            var initialContext = new ExecutionContext
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

            accessor.Set(initialContext);

            var engine = new AiSequentialExecutionEngine(
                store,
                contextStore,
                accessor,
                factory,
                services,
                pipelineExecutor,
                logger,
                cleanupService,
                cleanupOptions,
                stateReader,
                stateWriter);

            var record = await engine.CreateAsync("test-pipeline", "hello");

            // -----------------------------
            // Act
            // -----------------------------

            var finalRecord = await engine.ExecuteAllAsync(record.ExecutionId);
            var finalState = await store.GetStateAsync(record.ExecutionId);

            // -----------------------------
            // Assert
            // -----------------------------

            Assert.NotNull(finalState);

            Assert.True(finalRecord.IsTerminal);
            Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);

            Assert.Equal(2, finalRecord.CompletedSteps.Count);
            Assert.Contains("step-1", finalRecord.CompletedSteps);
            Assert.Contains("step-2", finalRecord.CompletedSteps);

            var result = finalState!.Steps["step-1"].Result;

            Assert.NotNull(result);
            Assert.Equal("processed", result!.Output);

            Assert.Equal(string.Empty, finalRecord.CurrentStep);
            Assert.Equal(2, finalRecord.CurrentStepIndex);
        }

        /// <summary>
        /// Payload resolver placeholder.
        ///
        /// This test only uses inline execution state values. If payload resolution occurs,
        /// the test should fail because that would indicate an unexpected execution path.
        /// </summary>
        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this ExecuteAll test.");
            }
        }
    }
}