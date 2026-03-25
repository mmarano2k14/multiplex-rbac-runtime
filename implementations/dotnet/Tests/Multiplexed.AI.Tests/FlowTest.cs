using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;
using System.Collections.Concurrent;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Integration tests for <see cref="AiExecutionEngine"/>.
    ///
    /// This test suite validates the full orchestration flow:
    /// - Execution creation (CreateAsync)
    /// - RBAC execution context seeding
    /// - Step execution via IAiStepExecutor
    /// - Execution state persistence
    /// - Result merging into execution state
    /// - RBAC context key rotation
    /// - Step progression and completion
    ///
    /// The goal is to ensure that the engine behaves deterministically
    /// and maintains consistency across orchestration, state, and context layers.
    /// </summary>
    public class AiExecutionEngineTests
    {
        /// <summary>
        /// Validates the complete happy path of the AI execution engine:
        /// - A new execution is created with an initial RBAC context
        /// - The first step is executed successfully
        /// - The result is merged into the execution state
        /// - The context key is rotated
        /// - The execution reaches a terminal (Completed) state
        /// </summary>
        [Fact]
        public async Task Create_And_ExecuteNext_Should_Run_Full_Flow()
        {
            // -----------------------------
            // Arrange
            // -----------------------------

            var step = new FakeStep("step-1");

            var steps = new List<IAiStep> { step };

            var store = new InMemoryExecutionStore();
            var contextStore = new InMemoryContextStore();
            var accessor = new InMemoryContextAccessor();
            var factory = new FakeExecutionContextFactory();
            var services = new ServiceCollection().BuildServiceProvider();
            var executor = new FakeStepExecutor();
            var logger = new NoopLogger();

            // Create a realistic RBAC execution context
            var context = new ExecutionContext
            {
                ContextKey = "",
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

            var engine = new AiExecutionEngine(
                steps,
                store,
                contextStore,
                accessor,
                factory,
                services,
                executor,
                logger
            );

            // -----------------------------
            // Act
            // -----------------------------

            var record = await engine.CreateAsync("hello");

            record = await engine.ExecuteNextAsync(record.ExecutionId);

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
            Assert.Equal("processed", state!.Get<string>("result"));
        }
    }
}