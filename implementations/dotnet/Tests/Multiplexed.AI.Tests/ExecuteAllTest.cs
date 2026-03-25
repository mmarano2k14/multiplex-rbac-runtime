using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;
using Multiplexed.Rbac.Core.ExecutionContext;
using System.Collections.Concurrent;
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

            var steps = new List<IAiStep>
            {
                new Step1(),
                new Step2()
            };

            var store = new InMemoryExecutionStore();
            var contextStore = new InMemoryContextStore();
            var accessor = new InMemoryContextAccessor();
            var factory = new FakeExecutionContextFactory();
            var services = new ServiceCollection().BuildServiceProvider();
            var executor = new PassThroughStepExecutor();
            var logger = new NoopLogger();

            var initialContext = new ExecutionContext
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

            accessor.Set(initialContext);

            var engine = new AiExecutionEngine(
                steps,
                store,
                contextStore,
                accessor,
                factory,
                services,
                executor,
                logger);

            var record = await engine.CreateAsync("hello");

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

            Assert.Equal("value-from-step-1", finalState!.Get<string>("step1-result"));
            Assert.Equal("value-from-step-2", finalState.Get<string>("step2-result"));

            Assert.Equal(string.Empty, finalRecord.CurrentStep);
            Assert.Equal(2, finalRecord.CurrentStepIndex);
        }

        /// <summary>
        /// First fake step used to validate multi-step progression.
        /// </summary>
        private sealed class Step1 : IAiStep
        {
            public string Name => "step-1";

            public Task<AiStepResult> ExecuteAsync(
                AiExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    AiStepResult.Ok(
                        output: "value-from-step-1",
                        data: new Dictionary<string, object?>
                        {
                            ["step1-result"] = "value-from-step-1"
                        }));
            }
        }

        /// <summary>
        /// Second fake step used to validate multi-step progression.
        /// </summary>
        private sealed class Step2 : IAiStep
        {
            public string Name => "step-2";

            public Task<AiStepResult> ExecuteAsync(
                AiExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    AiStepResult.Ok(
                        output: "value-from-step-2",
                        data: new Dictionary<string, object?>
                        {
                            ["step2-result"] = "value-from-step-2"
                        }));
            }
        }

        /// <summary>
        /// Pass-through executor used to keep the test focused on engine orchestration.
        /// </summary>
        private sealed class PassThroughStepExecutor : IAiStepExecutor
        {
            public Task<AiStepResult> ExecuteAsync(
                IAiStep step,
                AiExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return step.ExecuteAsync(context, cancellationToken);
            }
        }

        /// <summary>
        /// In-memory execution store used for integration testing.
        /// </summary>
        private sealed class InMemoryExecutionStore : IAiExecutionStore
        {
            private readonly ConcurrentDictionary<string, AiExecutionRecord> _records = new(StringComparer.Ordinal);
            private readonly ConcurrentDictionary<string, AiExecutionState> _states = new(StringComparer.Ordinal);

            public Task CreateAsync(
                AiExecutionRecord record,
                AiExecutionState state,
                CancellationToken cancellationToken = default)
            {
                _records[record.ExecutionId] = CloneRecord(record);
                _states[state.ExecutionId] = CloneState(state);
                return Task.CompletedTask;
            }

            public Task<AiExecutionRecord?> GetRecordAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                _records.TryGetValue(executionId, out var record);
                return Task.FromResult(record is null ? null : CloneRecord(record));
            }

            public Task<AiExecutionState?> GetStateAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                _states.TryGetValue(executionId, out var state);
                return Task.FromResult(state is null ? null : CloneState(state));
            }

            public Task<bool> TryUpdateAsync(
                string executionId,
                string expectedStepKey,
                AiExecutionRecord record,
                AiExecutionState state,
                CancellationToken cancellationToken = default)
            {
                if (!_records.TryGetValue(executionId, out var current))
                    return Task.FromResult(false);

                if (!string.Equals(current.ExecutionStepKey, expectedStepKey, StringComparison.Ordinal))
                    return Task.FromResult(false);

                _records[executionId] = CloneRecord(record);
                _states[executionId] = CloneState(state);

                return Task.FromResult(true);
            }

            private static AiExecutionRecord CloneRecord(AiExecutionRecord source)
            {
                return new AiExecutionRecord
                {
                    ExecutionId = source.ExecutionId,
                    ContextKey = source.ContextKey,
                    CurrentStepIndex = source.CurrentStepIndex,
                    Steps = new List<string>(source.Steps),
                    CompletedSteps = new List<string>(source.CompletedSteps),
                    ExecutionContextSnapshot = source.ExecutionContextSnapshot,
                    Status = source.Status,
                    Version = source.Version,
                    CurrentStep = source.CurrentStep,
                    ExecutionStepKey = source.ExecutionStepKey,
                    CreatedAtUtc = source.CreatedAtUtc,
                    UpdatedAtUtc = source.UpdatedAtUtc
                };
            }

            private static AiExecutionState CloneState(AiExecutionState source)
            {
                return new AiExecutionState
                {
                    ExecutionId = source.ExecutionId,
                    Data = new Dictionary<string, object?>(source.Data, StringComparer.Ordinal),
                    Metadata = new Dictionary<string, object?>(source.Metadata, StringComparer.Ordinal),
                    CreatedAtUtc = source.CreatedAtUtc,
                    UpdatedAtUtc = source.UpdatedAtUtc
                };
            }
        }

        /// <summary>
        /// In-memory RBAC context store used for integration testing.
        /// </summary>
        private sealed class InMemoryContextStore : IContextStore
        {
            private readonly ConcurrentDictionary<string, ExecutionContext> _store = new(StringComparer.Ordinal);

            public Task<string> StoreAsync(ExecutionContext context)
            {
                var key = Guid.NewGuid().ToString("N");
                context.ContextKey = key;
                _store[key] = context;
                return Task.FromResult(key);
            }

            public Task<ExecutionContext?> GetAsync(string key)
            {
                _store.TryGetValue(key, out var context);
                return Task.FromResult(context);
            }

            public Task<bool> TryAcquireInFlightAsync(string key, int maxInFlight) => Task.FromResult(true);

            public Task ReleaseInFlightAsync(string key) => Task.CompletedTask;

            public Task<(string newKey, ExecutionContext context)> RotateAsync(string key, TimeSpan overlapWindow)
            {
                if (!_store.TryGetValue(key, out var existing) || existing is null)
                    throw new InvalidOperationException("Context not found.");

                var rotated = CloneContext(existing);
                var newKey = Guid.NewGuid().ToString("N");
                rotated.ContextKey = newKey;

                _store[newKey] = rotated;

                return Task.FromResult((newKey, rotated));
            }

            public Task<string> SeedAsync(ExecutionContext context)
            {
                var key = Guid.NewGuid().ToString("N");
                context.ContextKey = key;
                _store[key] = context;
                return Task.FromResult(key);
            }

            private static ExecutionContext CloneContext(ExecutionContext source)
            {
                return new ExecutionContext
                {
                    ContextKey = source.ContextKey,
                    Project = source.Project,
                    TenantId = source.TenantId,
                    TenantGroupId = source.TenantGroupId,
                    CurrentNamespace = source.CurrentNamespace,
                    UserId = source.UserId,
                    Namespaces = source.Namespaces,
                    TtlSeconds = source.TtlSeconds
                };
            }
        }

        /// <summary>
        /// In-memory execution context accessor used for integration testing.
        /// </summary>
        private sealed class InMemoryContextAccessor : IExecutionContextAccessor
        {
            public ExecutionContext? Current { get; private set; }

            public void Set(ExecutionContext context) => Current = context;

            public void Clear() => Current = null;
        }

        /// <summary>
        /// Fake execution context factory used for integration testing.
        /// </summary>
        private sealed class FakeExecutionContextFactory : IExecutionContextFactory
        {
            public ExecutionContext CreateCopy(ExecutionContext context, string contextKey = "")
            {
                return new ExecutionContext
                {
                    ContextKey = contextKey,
                    Project = context.Project,
                    TenantId = context.TenantId,
                    TenantGroupId = context.TenantGroupId,
                    CurrentNamespace = context.CurrentNamespace,
                    UserId = context.UserId,
                    Namespaces = context.Namespaces,
                    TtlSeconds = context.TtlSeconds
                };
            }

            public ExecutionContextSnapshot CreateSnapshot(ExecutionContext context)
            {
                return new ExecutionContextSnapshot
                {
                    ContextKey = context.ContextKey,
                    Project = context.Project,
                    TenantId = context.TenantId,
                    TenantGroupId = context.TenantGroupId,
                    CurrentNamespace = context.CurrentNamespace,
                    UserId = context.UserId,
                    Namespaces = context.Namespaces
                };
            }
        }


    }
}