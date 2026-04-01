using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Integration tests for <see cref="AiSequentialExecutionEngine"/>.
    ///
    /// This test suite validates the full orchestration flow:
    /// - Execution creation (CreateAsync)
    /// - RBAC execution context seeding
    /// - Pipeline resolution through the pipeline executor
    /// - Step execution via IAiStepExecutor
    /// - Execution state persistence
    /// - Result merging into execution state
    /// - RBAC context key rotation
    /// - Step progression and completion
    ///
    /// The goal is to ensure that the engine behaves deterministically
    /// and maintains consistency across orchestration, state, and context layers.
    /// </summary>
    public sealed class AiExecutionEngineTests
    {
        /// <summary>
        /// Validates the complete happy path of the AI execution engine:
        /// - A new execution is created with an initial RBAC context
        /// - The configured pipeline step is executed successfully
        /// - The result is merged into the execution state
        /// - The context key is rotated
        /// - The execution reaches a terminal completed state
        /// </summary>
        [Fact]
        public async Task Create_And_ExecuteNext_Should_Run_Full_Flow()
        {
            // -----------------------------
            // Arrange
            // -----------------------------

            var step = new FakeStep("step-1");

            var store = new FakeInMemoryExecutionStore();
            var contextStore = new FakeInMemoryContextStore();
            var accessor = new FakeInMemoryContextAccessor();
            var factory = new FakeExecutionContextFactory();
            var services = new ServiceCollection().BuildServiceProvider();
            var executor = new FakeStepExecutor();
            var logger = new NoopLogger();

            var definitionProvider = new FakeInMemoryAiPipelineDefinitionProvider(
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
                            }
                        }
                    }
                });

            var sourceSelector = new FakeAiPipelineDefinitionSourceSelector(definitionProvider);

            var stepRegistry = new FakeInMemoryAiStepRegistry(
                new[]
                {
                    new KeyValuePair<string, Multiplexed.Abstractions.AI.Steps.IAiStep>(
                        "step-1",
                        step)
                });



            var resolver = new AiPipelineResolver(stepRegistry);

            var pipelineExecutor = new AiSequentialPipelineExecutor(
                sourceSelector,
                resolver,
                executor);

            // Create a realistic RBAC execution context
            var context = new ExecutionContext
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

            // Seed the runtime context (simulates authenticated request scope)
            accessor.Set(context);

            var cleanupService = new NoOpAiExecutionCleanupService();

            var cleanupOptions = Options.Create(new AiExecutionCleanupOptions
            {
                AutoCleanupOnCompleted = false,
                AutoCleanupOnFailed = false,
                SuppressCleanupExceptions = true
            });

            var engine = new AiSequentialExecutionEngine(
                store,
                contextStore,
                accessor,
                factory,
                services,
                pipelineExecutor,
                logger, cleanupService, cleanupOptions);

            // -----------------------------
            // Act
            // -----------------------------

            var record = await engine.CreateAsync("test-pipeline", "hello");

            record = await engine.ExecuteNextAsync(record.ExecutionId);
            var finalState = await store.GetStateAsync(record.ExecutionId);

            // -----------------------------
            // Assert
            // -----------------------------

            // Step execution completed
            Assert.Contains("step-1", record.CompletedSteps);

            // Execution reached terminal state
            Assert.True(record.IsTerminal);

            // Context key has been rotated and is not empty
            Assert.NotEmpty(record.ContextKey);

            // Validate execution state persistence and result merging
            var state = await store.GetStateAsync(record.ExecutionId);

            Assert.NotNull(state);
            AiStepResult? result = finalState!.Steps["step-1"].Result;
            Assert.Equal("processed", result?.Output);
        }
    }
}