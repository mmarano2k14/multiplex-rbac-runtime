using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Cleanup;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Execution.State;
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
    /// PURPOSE:
    /// - Validate the full orchestration lifecycle:
    ///   - Execution creation
    ///   - RBAC context propagation
    ///   - Step execution
    ///   - State persistence
    ///   - Result merging
    ///   - Context key rotation
    ///   - Terminal convergence
    ///
    /// ARCHITECTURE:
    /// - State mutation is handled by <see cref="IAiExecutionStateWriter"/>.
    /// - State reading is handled by <see cref="IAiExecutionStateReader"/>.
    /// - <see cref="AiExecutionState"/> is a persistence model only.
    /// </summary>
    public sealed class AiExecutionEngineTests
    {
        /// <summary>
        /// Validates the complete happy path:
        /// - Execution is created
        /// - Step executes successfully
        /// - Result is persisted
        /// - Context key rotates
        /// - Execution reaches terminal state
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

            // 🔥 NEW: state boundaries
            IAiExecutionStateWriter stateWriter = new DefaultAiExecutionStateWriter();
            IAiExecutionStateReader stateReader = new DefaultAiExecutionStateReader(
                new NoopPayloadResolver());

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
                    new KeyValuePair<string, IAiStep>("step-1", step)
                });

            var resolver = new AiPipelineResolver(stepRegistry);

            var pipelineExecutor = new AiSequentialPipelineExecutor(
                sourceSelector,
                resolver,
                executor);

            // Simulated RBAC execution context
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
                logger,
                cleanupService,
                cleanupOptions,
                stateReader,
                stateWriter);

            // -----------------------------
            // Act
            // -----------------------------

            var record = await engine.CreateAsync("test-pipeline", "hello");

            record = await engine.ExecuteNextAsync(record.ExecutionId);
            var finalState = await store.GetStateAsync(record.ExecutionId);

            // -----------------------------
            // Assert
            // -----------------------------

            Assert.Contains("step-1", record.CompletedSteps);

            Assert.True(record.IsTerminal);

            Assert.NotEmpty(record.ContextKey);

            Assert.NotNull(finalState);

            var result = finalState!.Steps["step-1"].Result;

            Assert.NotNull(result);
            Assert.Equal("processed", result!.Output);
        }

        /// <summary>
        /// Minimal payload resolver used for tests.
        ///
        /// This test suite only uses inline state values.
        /// If payload resolution is triggered, it indicates an invalid test path.
        /// </summary>
        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this test.");
            }
        }
    }
}