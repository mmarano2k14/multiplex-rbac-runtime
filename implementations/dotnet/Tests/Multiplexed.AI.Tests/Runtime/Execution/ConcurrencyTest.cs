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
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Integration tests for concurrent execution behavior in <see cref="AiSequentialExecutionEngine"/>.
    ///
    /// PURPOSE:
    /// - Validates optimistic concurrency protection.
    /// - Ensures two concurrent callers cannot advance the same execution step twice.
    /// - Verifies the engine uses the execution state writer/reader boundary correctly.
    ///
    /// EXPECTATION:
    /// - One caller succeeds.
    /// - One caller fails due to optimistic concurrency conflict.
    /// - The step result is persisted exactly once.
    /// </summary>
    public sealed class AiExecutionEngineConcurrencyTests
    {
        [Fact]
        public async Task ExecuteNextAsync_Should_Allow_Only_One_Concurrent_Step_Transition()
        {
            // -----------------------------
            // Arrange
            // -----------------------------

            var step = new DelayedStep("step-1", delayMs: 100);

            var store = new FakeInMemoryExecutionStore();
            var contextStore = new FakeInMemoryContextStore();
            var accessor = new FakeInMemoryContextAccessor();
            var factory = new FakeExecutionContextFactory();
            var services = new ServiceCollection().BuildServiceProvider();
            var executor = new FakeStepExecutor();
            var logger = new NoopLogger();
            var cleanupService = new NoOpAiExecutionCleanupService();

            IAiExecutionStateWriter stateWriter = new DefaultAiExecutionStateWriter();
            IAiExecutionStateReader stateReader = new DefaultAiExecutionStateReader(
                new NoopPayloadResolver());

            var cleanupOptions = Options.Create(new AiExecutionCleanupOptions
            {
                AutoCleanupOnCompleted = false,
                AutoCleanupOnFailed = false,
                SuppressCleanupExceptions = true
            });

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
                cleanupOptions,stateReader,
                stateWriter);

            var record = await engine.CreateAsync("test-pipeline", "hello");

            // -----------------------------
            // Act
            // -----------------------------

            var task1 = ExecuteAndCaptureAsync(engine, record.ExecutionId);
            var task2 = ExecuteAndCaptureAsync(engine, record.ExecutionId);

            var outcomes = await Task.WhenAll(task1, task2);

            // -----------------------------
            // Assert
            // -----------------------------

            var successCount = outcomes.Count(x => x.Exception is null);
            var failureCount = outcomes.Count(x => x.Exception is InvalidOperationException);

            Assert.Equal(1, successCount);
            Assert.Equal(1, failureCount);

            var finalRecord = await store.GetRecordAsync(record.ExecutionId);
            var finalState = await store.GetStateAsync(record.ExecutionId);

            Assert.NotNull(finalRecord);
            Assert.NotNull(finalState);

            Assert.Single(finalRecord!.CompletedSteps);
            Assert.Contains("step-1", finalRecord.CompletedSteps);

            var result = finalState!.Steps["step-1"].Result;

            Assert.NotNull(result);
            Assert.Equal("processed", result!.Output);
            Assert.True(finalRecord.IsTerminal);
        }

        /// <summary>
        /// Executes one concurrent attempt and captures the exception instead of failing the test task.
        /// </summary>
        private static async Task<ExecutionAttemptOutcome> ExecuteAndCaptureAsync(
            AiSequentialExecutionEngine engine,
            string executionId)
        {
            try
            {
                var record = await engine.ExecuteNextAsync(executionId);
                return new ExecutionAttemptOutcome(record, null);
            }
            catch (Exception ex)
            {
                return new ExecutionAttemptOutcome(null, ex);
            }
        }

        /// <summary>
        /// Fake step that delays execution to increase overlap between concurrent callers.
        /// </summary>
        private sealed class DelayedStep : IAiStep
        {
            private readonly int _delayMs;

            public DelayedStep(string name, int delayMs)
            {
                Name = name;
                _delayMs = delayMs;
            }

            public string Name { get; }

            public async Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                await Task.Delay(_delayMs, cancellationToken);

                return AiStepResult.Ok(
                    output: "processed",
                    data: new Dictionary<string, object?>
                    {
                        ["result"] = "processed"
                    });
            }
        }

        /// <summary>
        /// Payload resolver placeholder.
        ///
        /// This test only uses inline state values, so resolving payloads would indicate
        /// an unexpected test path.
        /// </summary>
        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this concurrency test.");
            }
        }

        private sealed record ExecutionAttemptOutcome(
            AiExecutionRecord? Record,
            Exception? Exception);
    }
}