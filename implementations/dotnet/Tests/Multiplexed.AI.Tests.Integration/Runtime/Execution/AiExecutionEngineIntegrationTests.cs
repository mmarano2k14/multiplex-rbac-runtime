using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Abstractions;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline.Steps;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Stores.Memory;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.AI.Tests.Integration.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime;
using Multiplexed.Rbac.Core.Stores;
using Multiplexed.Rbac.Core.Stores.Cache;
using Multiplexed.Rbac.Core.Stores.Memory;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// End-to-end integration tests for <see cref="AiExecutionEngine"/>.
    ///
    /// These tests validate the persisted orchestration flow using:
    /// - the real AI execution engine
    /// - the real Redis-backed execution store
    /// - either a fake or real RBAC context store depending on the scenario
    /// - a fake pipeline executor using real or controlled test steps
    ///
    /// Covered behaviors:
    /// - execution creation
    /// - step-by-step progression
    /// - ExecuteAll orchestration
    /// - persisted state propagation
    /// - failure result handling
    /// - exception path handling
    /// - terminal execution behavior
    /// </summary>
    [Collection("redis")]
    public sealed class AiExecutionEngineIntegrationTests
    {
        private readonly IConnectionMultiplexer _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionEngineIntegrationTests"/> class.
        /// </summary>
        public AiExecutionEngineIntegrationTests(RedisFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            _connection = fixture.Connection;
        }

        /// <summary>
        /// Verifies that a two-step pipeline can execute from start to finish
        /// and persist its state correctly after each transition.
        /// This version uses the lightweight fake context store.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_Should_Run_Full_Pipeline_And_Complete()
        {
            // Arrange
            const string pipelineName = "pipeline-full";
            var engine = CreateEngine(
                pipelineName,
                new TestStepDefinition(
                    Name: "hello",
                    StepKey: "hello-world",
                    Order: 1,
                    Input: new Dictionary<string, object?> { ["text"] = "Marco" }),
                new TestStepDefinition(
                    Name: "summary",
                    StepKey: "summary",
                    Order: 2));

            var execution = await engine.CreateAsync(
                pipelineName,
                "Marco",
                CancellationToken.None);

            try
            {
                // Act - execute step 1
                var afterStep1 = await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);

                // Assert - persisted progress after step 1
                Assert.Equal(AiExecutionStatus.Running, afterStep1.Status);
                Assert.Equal(1, afterStep1.CurrentStepIndex);
                Assert.Equal("summary", afterStep1.CurrentStep);
                Assert.Contains("hello", afterStep1.CompletedSteps);

                var persistedAfterStep1State = await GetStore().GetStateAsync(execution.ExecutionId, CancellationToken.None);
                Assert.NotNull(persistedAfterStep1State);
                Assert.Equal("Hello World : Marco", persistedAfterStep1State!.Get<string>("message"));

                // Act - execute step 2
                var finalRecord = await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);

                // Assert - completed
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
                Assert.Equal(2, finalRecord.CompletedSteps.Count);
                Assert.Contains("hello", finalRecord.CompletedSteps);
                Assert.Contains("summary", finalRecord.CompletedSteps);

                var persistedFinalState = await GetStore().GetStateAsync(execution.ExecutionId, CancellationToken.None);
                Assert.NotNull(persistedFinalState);

                // SummaryStep injects its output back into AiExecutionKeys.Input.
                var summarized = persistedFinalState!.Get<string>(AiExecutionKeys.Input);
                Assert.False(string.IsNullOrWhiteSpace(summarized));
                Assert.Contains("Summarize the following content", summarized);
            }
            finally
            {
                await CleanupExecutionAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Verifies that the same happy-path pipeline also succeeds when the engine
        /// is wired with the real RBAC context store instead of the fake test store.
        ///
        /// This test is intentionally narrow:
        /// - it mirrors the standard full pipeline happy path
        /// - it validates real AI + RBAC context-store integration
        /// - it does not replace the rest of the suite, which remains focused
        ///   on AI orchestration behavior
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_Should_Run_Full_Pipeline_And_Complete_With_Real_ContextStore()
        {
            // Arrange
            const string pipelineName = "pipeline-full-real-context";
            var engine = CreateEngineWithRealContextStore(
                pipelineName,
                new TestStepDefinition(
                    Name: "hello",
                    StepKey: "hello-world",
                    Order: 1,
                    Input: new Dictionary<string, object?> { ["text"] = "Marco" }),
                new TestStepDefinition(
                    Name: "summary",
                    StepKey: "summary",
                    Order: 2));

            var execution = await engine.CreateAsync(
                pipelineName,
                "Marco",
                CancellationToken.None);

            try
            {
                // Act - execute step 1
                var afterStep1 = await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);

                // Assert - persisted progress after step 1
                Assert.Equal(AiExecutionStatus.Running, afterStep1.Status);
                Assert.Equal(1, afterStep1.CurrentStepIndex);
                Assert.Equal("summary", afterStep1.CurrentStep);
                Assert.Contains("hello", afterStep1.CompletedSteps);

                var persistedAfterStep1State = await GetStore().GetStateAsync(execution.ExecutionId, CancellationToken.None);
                Assert.NotNull(persistedAfterStep1State);
                Assert.Equal("Hello World : Marco", persistedAfterStep1State!.Get<string>("message"));

                // Act - execute step 2
                var finalRecord = await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);

                // Assert - completed
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
                Assert.Equal(2, finalRecord.CompletedSteps.Count);
                Assert.Contains("hello", finalRecord.CompletedSteps);
                Assert.Contains("summary", finalRecord.CompletedSteps);

                var persistedFinalState = await GetStore().GetStateAsync(execution.ExecutionId, CancellationToken.None);
                Assert.NotNull(persistedFinalState);

                var summarized = persistedFinalState!.Get<string>(AiExecutionKeys.Input);
                Assert.False(string.IsNullOrWhiteSpace(summarized));
                Assert.Contains("Summarize the following content", summarized);
            }
            finally
            {
                await CleanupExecutionAndContextAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Verifies that <see cref="AiExecutionEngine.ExecuteAllAsync"/> runs
        /// the whole remaining pipeline and ends in a completed terminal state.
        /// </summary>
        [RedisFact]
        public async Task ExecuteAllAsync_Should_Run_Remaining_Pipeline_To_Completion()
        {
            // Arrange
            const string pipelineName = "pipeline-execute-all";
            var engine = CreateEngine(
                pipelineName,
                new TestStepDefinition(
                    Name: "hello",
                    StepKey: "hello-world",
                    Order: 1,
                    Input: new Dictionary<string, object?> { ["text"] = "Bangkok 2" }),
                new TestStepDefinition(
                    Name: "summary",
                    StepKey: "summary",
                    Order: 2));

            var execution = await engine.CreateAsync(
                pipelineName,
                "Bangkok",
                CancellationToken.None);

            try
            {
                // Act
                var finalRecord = await engine.ExecuteAllAsync(execution.ExecutionId, CancellationToken.None);

                // Assert
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
                Assert.Equal(2, finalRecord.CompletedSteps.Count);

                var finalState = await GetStore().GetStateAsync(execution.ExecutionId, CancellationToken.None);
                Assert.NotNull(finalState);
                AiStepResult? result = finalState!.Steps["hello"].Result;
                Assert.Equal("Hello World : Bangkok 2", result?.Output);
            }
            finally
            {
                await CleanupExecutionAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Verifies that ExecuteAllAsync also succeeds when using the real RBAC context store.
        /// This ensures full orchestration works with real context persistence.
        /// </summary>
        [RedisFact]
        public async Task ExecuteAllAsync_Should_Run_To_Completion_With_Real_ContextStore()
        {
            // Arrange
            const string pipelineName = "pipeline-execute-all-real-context";

            var engine = CreateEngineWithRealContextStore(
                pipelineName,
                new TestStepDefinition(
                    Name: "hello",
                    StepKey: "hello-world",
                    Order: 1,
                    Input: new Dictionary<string, object?> { ["text"] = AiExecutionKeys.Input }),
                new TestStepDefinition(
                    Name: "summary",
                    StepKey: "summary",
                    Order: 2));

            var execution = await engine.CreateAsync(
                pipelineName,
                "Phuket",
                CancellationToken.None);

            try
            {
                // Act
                var finalRecord = await engine.ExecuteAllAsync(
                    execution.ExecutionId,
                    CancellationToken.None);

                // Assert
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
                Assert.Equal(2, finalRecord.CompletedSteps.Count);

                var finalState = await GetStore().GetStateAsync(
                    execution.ExecutionId,
                    CancellationToken.None);

                Assert.NotNull(finalState);

                var message = finalState!.Get<string>("message");
                Assert.Equal("Hello World : Phuket", message);

                var summary = finalState.Get<string>(AiExecutionKeys.Input);
                Assert.False(string.IsNullOrWhiteSpace(summary));
            }
            finally
            {
                await CleanupExecutionAndContextAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Verifies that a failing step result marks the execution as failed
        /// without requiring an exception to be thrown.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_Should_Mark_Execution_As_Failed_When_Step_Returns_Failed_Result()
        {
            // Arrange
            const string pipelineName = "pipeline-fail-result";
            var engine = CreateEngine(
                pipelineName,
                new TestStepDefinition(
                    Name: "fail",
                    StepKey: "always-fail",
                    Order: 1));

            var execution = await engine.CreateAsync(
                pipelineName,
                "input",
                CancellationToken.None);

            try
            {
                // Act
                var failedRecord = await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);

                // Assert
                Assert.Equal(AiExecutionStatus.Failed, failedRecord.Status);
                Assert.Empty(failedRecord.CompletedSteps);

                var persisted = await GetStore().GetRecordAsync(execution.ExecutionId, CancellationToken.None);
                Assert.NotNull(persisted);
                Assert.Equal(AiExecutionStatus.Failed, persisted!.Status);
            }
            finally
            {
                await CleanupExecutionAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Verifies that an exception thrown by a step is rethrown by the engine
        /// and also persisted as a failed execution state.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_Should_Mark_Execution_As_Failed_When_Step_Throws()
        {
            // Arrange
            const string pipelineName = "pipeline-throw";
            var engine = CreateEngine(
                pipelineName,
                new TestStepDefinition(
                    Name: "throw",
                    StepKey: "throwing-step",
                    Order: 1));

            var execution = await engine.CreateAsync(
                pipelineName,
                "input",
                CancellationToken.None);

            try
            {
                // Act / Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None));

                var persisted = await GetStore().GetRecordAsync(execution.ExecutionId, CancellationToken.None);
                Assert.NotNull(persisted);
                Assert.Equal(AiExecutionStatus.Failed, persisted!.Status);
            }
            finally
            {
                await CleanupExecutionAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Verifies that once an execution is terminal, calling ExecuteNextAsync again
        /// returns the same terminal record without mutating it.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_Should_Return_Terminal_Record_When_Already_Completed()
        {
            // Arrange
            const string pipelineName = "pipeline-terminal";
            var engine = CreateEngine(
                pipelineName,
                new TestStepDefinition(
                    Name: "hello",
                    StepKey: "hello-world",
                    Order: 1,
                    Input: new Dictionary<string, object?> { ["text"] = AiExecutionKeys.Input }));

            var execution = await engine.CreateAsync(
                pipelineName,
                "Phuket",
                CancellationToken.None);

            try
            {
                var completed = await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);
                Assert.Equal(AiExecutionStatus.Completed, completed.Status);

                // Act
                var again = await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);

                // Assert
                Assert.Equal(AiExecutionStatus.Completed, again.Status);
                Assert.Equal(completed.ExecutionId, again.ExecutionId);
                Assert.Equal(completed.Version, again.Version);
                Assert.Equal(completed.ExecutionStepKey, again.ExecutionStepKey);
            }
            finally
            {
                await CleanupExecutionAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Verifies that the orchestration record and mutable state remain aligned
        /// after persisted transitions.
        /// </summary>
        [RedisFact]
        public async Task ExecuteNextAsync_Should_Keep_Record_And_State_Aligned_Across_Persistence()
        {
            // Arrange
            const string pipelineName = "pipeline-alignment";
            var engine = CreateEngine(
                pipelineName,
                new TestStepDefinition(
                    Name: "hello",
                    StepKey: "hello-world",
                    Order: 1,
                    Input: new Dictionary<string, object?> { ["text"] = "Sage" }),
                new TestStepDefinition(
                    Name: "summary",
                    StepKey: "summary",
                    Order: 2));

            var execution = await engine.CreateAsync(
                pipelineName,
                "Sage",
                CancellationToken.None);

            try
            {
                // Act
                await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);
                await engine.ExecuteNextAsync(execution.ExecutionId, CancellationToken.None);

                // Assert
                var storedRecord = await GetStore().GetRecordAsync(execution.ExecutionId, CancellationToken.None);
                var storedState = await GetStore().GetStateAsync(execution.ExecutionId, CancellationToken.None);

                Assert.NotNull(storedRecord);
                Assert.NotNull(storedState);

                Assert.Equal(storedRecord!.ExecutionId, storedState!.ExecutionId);
                Assert.Equal(storedRecord.PipelineName, storedState.PipelineName);
                Assert.Equal(AiExecutionStatus.Completed, storedRecord.Status);
                Assert.Equal("Hello World : Sage", storedState.Get<string>("message"));
            }
            finally
            {
                await CleanupExecutionAsync(execution.ExecutionId);
            }
        }

        /// <summary>
        /// Creates a fully wired engine using the real persisted execution store
        /// and controlled fake dependencies for context, pipeline execution, and logging.
        /// </summary>
        private AiExecutionEngine CreateEngine(
            string pipelineName,
            params TestStepDefinition[] steps)
        {
            var store = GetStore();
            var contextStore = new FakeContextStore();
            var accessor = new ExecutionContextAccessor();
            var contextFactory = new ExecutionContextFactory();
            var services = new ServiceCollection().BuildServiceProvider();
            var pipelineExecutor = new FakeAiPipelineExecutor(pipelineName, steps);
            var logger = new NoopLogger();

            accessor.Set(BuildTestExecutionContext()); 

            return new AiExecutionEngine(
                store,
                contextStore,
                accessor,
                contextFactory,
                services,
                pipelineExecutor,
                logger);
        }

        /// <summary>
        /// Creates a fully wired engine using the real persisted execution store
        /// and the real composite RBAC context store.
        /// </summary>
        private AiExecutionEngine CreateEngineWithRealContextStore(
            string pipelineName,
            params TestStepDefinition[] steps)
        {
            var store = GetStore();
            var contextStore = CreateRealContextStore();
            var accessor = new ExecutionContextAccessor();
            var contextFactory = new ExecutionContextFactory();
            var services = new ServiceCollection().BuildServiceProvider();
            var pipelineExecutor = new FakeAiPipelineExecutor(pipelineName, steps);
            var logger = new NoopLogger();

            accessor.Set(BuildTestExecutionContext());

            return new AiExecutionEngine(
                store,
                contextStore,
                accessor,
                contextFactory,
                services,
                pipelineExecutor,
                logger);
        }

        /// <summary>
        /// Creates the composite execution store used by the integration engine.
        /// Redis is primary, memory is fallback.
        /// </summary>
        private IAiExecutionStore GetStore()
        {
            var redis = new RedisAiExecutionStore(_connection);
            var memory = new MemoryAiExecutionStore();
            return new AiExecutionStore(redis, memory);
        }

        /// <summary>
        /// Creates the real composite RBAC context store used by the dedicated
        /// AI + RBAC integration test.
        /// </summary>
        private IContextStore CreateRealContextStore()
        {
            var options = Options.Create(new ContextRuntimeOptions
            {
                UseRedisLuaScriptShaCaching = true
            });

            var _cache = new MemoryCache(new MemoryCacheOptions());
            var _ttl = TimeSpan.FromMinutes(10); // valeur safe par défaut

            var redis = new RedisContextStore(_connection, options);
            var memory = new MemoryContextStore(_cache, _ttl);

            return new CompositeContextStore(redis, memory);
        }

        /// <summary>
        /// Builds a minimal but valid execution context for integration tests.
        /// </summary>
        private static ExecutionContext BuildTestExecutionContext()
        {
            return new ExecutionContext
            {
                ContextKey = "ctx_seed_test",
                Project = "Project",
                TenantId = "tenant-id-xxxx",
                TenantGroupId = "tenant-group-id-xxx",
                CurrentNamespace = "Namespace",
                UserId = "user-id",
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

        /// <summary>
        /// Removes persisted Redis keys for the supplied execution id.
        /// This keeps tests isolated and repeatable.
        /// </summary>
        private async Task CleanupExecutionAsync(string executionId)
        {
            var db = _connection.GetDatabase();
            await db.KeyDeleteAsync(GetExecutionRecordKey(executionId));
            await db.KeyDeleteAsync(GetExecutionStateKey(executionId));
        }

        /// <summary>
        /// Removes both AI execution keys and known RBAC context keys associated
        /// with the execution lifecycle.
        /// </summary>
        private async Task CleanupExecutionAndContextAsync(string executionId)
        {
            var store = GetStore();
            var record = await store.GetRecordAsync(executionId, CancellationToken.None);

            string? currentContextKey = record?.ContextKey;

            await CleanupExecutionAsync(executionId);

            if (!string.IsNullOrWhiteSpace(currentContextKey))
            {
                await CleanupContextAsync(currentContextKey);
            }

            // Also clean the initial seed key used in tests if it still exists.
            await CleanupContextAsync("ctx_seed_test");
        }

        /// <summary>
        /// Removes Redis keys associated with a given RBAC context key.
        /// </summary>
        private async Task CleanupContextAsync(string contextKey)
        {
            var db = _connection.GetDatabase();

            await db.KeyDeleteAsync(GetRbacContextKey(contextKey));
            await db.KeyDeleteAsync(GetRbacInFlightKey(contextKey));
        }

        /// <summary>
        /// Builds the Redis key used for AI execution records.
        /// </summary>
        private static string GetExecutionRecordKey(string executionId)
            => $"ai:execution:record:{executionId}";

        /// <summary>
        /// Builds the Redis key used for AI execution states.
        /// </summary>
        private static string GetExecutionStateKey(string executionId)
            => $"ai:execution:state:{executionId}";

        /// <summary>
        /// Builds the Redis key used for RBAC context payload.
        /// </summary>
        private static string GetRbacContextKey(string contextKey)
            => $"ac:ctx:{contextKey}";

        /// <summary>
        /// Builds the Redis key used for RBAC in-flight tracking.
        /// </summary>
        private static string GetRbacInFlightKey(string contextKey)
            => $"ac:inflight:{contextKey}";

        // ------------------------------------------------------------------
        // TEST INFRASTRUCTURE
        // ------------------------------------------------------------------

        /// <summary>
        /// Declarative test step definition used by the fake pipeline executor.
        /// This allows tests to define a runtime pipeline without depending on
        /// an external pipeline definition provider.
        /// </summary>
        private sealed record TestStepDefinition(
            string Name,
            string StepKey,
            int Order,
            IReadOnlyDictionary<string, object?>? Input = null,
            IReadOnlyDictionary<string, object?>? Config = null);

        /// <summary>
        /// Fake pipeline executor that prepares a runtime pipeline and executes
        /// exactly one current step per invocation.
        ///
        /// This keeps the tests focused on engine orchestration behavior while still
        /// exercising real step logic.
        /// </summary>
        private sealed class FakeAiPipelineExecutor : IAiPipelineExecutor
        {
            private readonly string _pipelineName;
            private readonly IReadOnlyList<TestStepDefinition> _steps;

            public FakeAiPipelineExecutor(
                string pipelineName,
                IEnumerable<TestStepDefinition> steps)
            {
                _pipelineName = pipelineName;
                _steps = steps.OrderBy(x => x.Order).ToArray();
            }

            public Task<ResolvedAiPipeline> PrepareAsync(
                string pipelineName,
                CancellationToken cancellationToken = default)
            {
                if (!string.Equals(_pipelineName, pipelineName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Unknown pipeline '{pipelineName}'.");
                }

                var resolved = new ResolvedAiPipeline
                {
                    Name = pipelineName,
                    Version = "1",
                    Steps = _steps
                        .Select(x => new ResolvedAiPipelineStep
                        {
                            Name = x.Name,
                            StepKey = x.StepKey,
                            Order = x.Order,
                            Step = ResolveStep(x.StepKey),
                            Input = x.Input ?? new Dictionary<string, object?>(),
                            Config = x.Config ?? new Dictionary<string, object?>()
                        })
                        .ToArray()
                };

                return Task.FromResult(resolved);
            }

            public async Task<PipelineExecutionResult> ExecuteNextAsync(
                ResolvedAiPipeline pipeline,
                AiExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                var orderedSteps = pipeline.Steps.OrderBy(x => x.Order).ToArray();
                var currentIndex = context.Record.CurrentStepIndex;

                if (currentIndex < 0 || currentIndex >= orderedSteps.Length)
                {
                    throw new InvalidOperationException(
                        $"Execution step index '{currentIndex}' is outside the pipeline bounds.");
                }

                var currentStep = orderedSteps[currentIndex];
                SetResolvedStepMetadata(context.State, currentStep);

                context.State.EnsureStepInitialized(currentStep);

                var result = await currentStep.Step.ExecuteAsync(context, cancellationToken);

                context.State.SetStepResult(currentStep.Name, result);

                


                var nextStepIndex = currentIndex + 1;
                var isCompleted = nextStepIndex >= orderedSteps.Length;

                return new PipelineExecutionResult
                {
                    PipelineName = pipeline.Name,
                    Steps = orderedSteps.Select(x => x.Name).ToArray(),
                    ExecutedStepName = currentStep.Name,
                    ExecutedStepIndex = currentIndex,
                    NextStepIndex = nextStepIndex,
                    NextStepName = isCompleted ? null : orderedSteps[nextStepIndex].Name,
                    IsCompleted = isCompleted,
                    StepResult = result
                };
            }

            private static IAiStep ResolveStep(string stepKey)
            {
                return stepKey switch
                {
                    "hello-world" => new HelloWorldStep(),
                    "summary" => new SummaryStep(new FakeAiService()),
                    "always-fail" => new AlwaysFailStep(),
                    "throwing-step" => new ThrowingStep(),
                    _ => throw new InvalidOperationException($"Unknown step key '{stepKey}'.")
                };
            }
        }

        /// <summary>
        /// Fake AI service used by <see cref="SummaryStep"/>.
        /// </summary>
        private sealed class FakeAiService : IAiService
        {
            public Task<AiResponse> CompleteAsync(
                AiRequest request,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AiResponse
                {
                    Content = $"SUMMARY::{request.Prompt}"
                });
            }
        }

        /// <summary>
        /// Step returning an unsuccessful result without throwing.
        /// Useful for validating failure-result behavior.
        /// </summary>
        [AiStep("always-fail")]
        private sealed class AlwaysFailStep : IAiStep
        {
            public string Name => "always-fail";

            public Task<AiStepResult> ExecuteAsync(
                AiExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AiStepResult.Fail("Forced failure."));
            }
        }

        /// <summary>
        /// Step throwing an exception to validate the engine exception path.
        /// </summary>
        [AiStep("throwing-step")]
        private sealed class ThrowingStep : IAiStep
        {
            public string Name => "throwing-step";

            public Task<AiStepResult> ExecuteAsync(
                AiExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Forced exception.");
            }
        }

        /// <summary>
        /// Injects the current resolved step metadata into the execution state.
        /// This allows the concrete step implementation to access its declarative
        /// input and configuration without changing the IAiStep contract.
        /// </summary>
        private static void SetResolvedStepMetadata(
            AiExecutionState state,
            ResolvedAiPipelineStep resolvedStep)
        {
            state.Metadata[AiExecutionKeys.CurrentStepName] = resolvedStep.Name;
            state.Metadata[AiExecutionKeys.CurrentStepKey] = resolvedStep.StepKey;
            state.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}