using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Pipeline;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.AI.Tests.Models;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Integration tests for concurrent execution behavior in <see cref="AiSequentialExecutionEngine"/>.
    ///
    /// This test suite validates that optimistic concurrency prevents double progression
    /// when multiple callers attempt to execute the same workflow step at the same time.
    /// </summary>
    public sealed class AiExecutionEngineConcurrencyTests
    {
        /// <summary>
        /// Validates that only one concurrent execution succeeds when two callers
        /// attempt to execute the same step with the same execution step key.
        /// </summary>
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
                logger);

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

            AiStepResult? result = finalState!.Steps["step-1"].Result;
            Assert.Equal("processed", result?.Output);
            Assert.True(finalRecord.IsTerminal);
        }

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
        /// Fake step that delays slightly in order to increase the likelihood
        /// of concurrent overlap during execution.
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

        private sealed record ExecutionAttemptOutcome(
            AiExecutionRecord? Record,
            Exception? Exception);
    }
}