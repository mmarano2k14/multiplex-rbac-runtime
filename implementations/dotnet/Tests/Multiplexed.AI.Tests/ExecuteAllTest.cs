using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Integration tests for <see cref="AiExecutionEngine.ExecuteAllAsync"/>.
    ///
    /// This test suite validates full multi-step orchestration, including:
    /// - execution creation
    /// - sequential step progression
    /// - state merging across multiple steps
    /// - terminal completion state
    /// </summary>
    public sealed class AiExecutionEngineExecuteAllTests
    {
        /// <summary>
        /// Validates that <see cref="AiExecutionEngine.ExecuteAllAsync"/> executes
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

            var stepRegistry = new InMemoryAiStepRegistry(
                new[]
                {
                    new KeyValuePair<string, Multiplexed.Abstractions.AI.Steps.IAiStep>("step-1", step1),
                    new KeyValuePair<string, Multiplexed.Abstractions.AI.Steps.IAiStep>("step-2", step2)
                });

            var resolver = new AiPipelineResolver(stepRegistry);
            var pipelineExecutor = new AiPipelineExecutor(
                definitionProvider,
                resolver,
                executor);

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

            var engine = new AiExecutionEngine(
                store,
                contextStore,
                accessor,
                factory,
                services,
                pipelineExecutor,
                logger);

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

            Assert.Equal("processed", finalState!.Get<string>("result"));

            Assert.Equal(string.Empty, finalRecord.CurrentStep);
            Assert.Equal(2, finalRecord.CurrentStepIndex);
        }
    }
}