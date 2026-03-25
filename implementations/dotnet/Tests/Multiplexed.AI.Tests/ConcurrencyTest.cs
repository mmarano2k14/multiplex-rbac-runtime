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
    /// Integration tests for concurrent execution behavior in <see cref="AiExecutionEngine"/>.
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
            // Arrange
            var step = new DelayedStep("step-1", delayMs: 100);

            var steps = new List<IAiStep> { step };

            var store = new ConcurrencyAwareExecutionStore();
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

            // Act
            var task1 = ExecuteAndCaptureAsync(engine, record.ExecutionId);
            var task2 = ExecuteAndCaptureAsync(engine, record.ExecutionId);

            await Task.WhenAll(task1, task2);

            ExecutionAttemptOutcome[] outcomes = await Task.WhenAll(task1, task2);

            // Assert
            var successCount = outcomes.Count(x => x.Exception is null);
            var failureCount = outcomes.Count(x => x.Exception is InvalidOperationException);

            Assert.Equal(1, successCount);
            Assert.Equal(1, failureCount);

            var finalRecord = await store.GetRecordAsync(record.ExecutionId);
            var finalState = await store.GetStateAsync(record.ExecutionId);

            Assert.NotNull(finalRecord);
            Assert.NotNull(finalState);

            // Step must be completed only once
            Assert.Single(finalRecord!.CompletedSteps);
            Assert.Contains("step-1", finalRecord.CompletedSteps);

            // State must be merged only once
            Assert.Equal("processed", finalState!.Get<string>("result"));

            // Final execution must be terminal
            Assert.True(finalRecord.IsTerminal);
        }

        private static async Task<ExecutionAttemptOutcome> ExecuteAndCaptureAsync(
            AiExecutionEngine engine,
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
                AiExecutionContext context,
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
        /// Pass-through executor that delegates directly to the step.
        /// This keeps the test focused on engine-level concurrency behavior.
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
        /// In-memory execution store that enforces optimistic concurrency
        /// by validating the expected execution step key.
        /// </summary>
        private sealed class ConcurrencyAwareExecutionStore : IAiExecutionStore
        {
            private readonly ConcurrentDictionary<string, AiExecutionRecord> _records = new(StringComparer.Ordinal);
            private readonly ConcurrentDictionary<string, AiExecutionState> _states = new(StringComparer.Ordinal);
            private readonly object _gate = new();

            public Task CreateAsync(
                AiExecutionRecord record,
                AiExecutionState state,
                CancellationToken cancellationToken = default)
            {
                _records[record.ExecutionId] = record;
                _states[state.ExecutionId] = state;
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
                lock (_gate)
                {
                    if (!_records.TryGetValue(executionId, out var current))
                        return Task.FromResult(false);

                    if (!string.Equals(current.ExecutionStepKey, expectedStepKey, StringComparison.Ordinal))
                        return Task.FromResult(false);

                    _records[executionId] = CloneRecord(record);
                    _states[executionId] = CloneState(state);

                    return Task.FromResult(true);
                }
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

        private sealed class InMemoryContextAccessor : IExecutionContextAccessor
        {
            public ExecutionContext? Current { get; private set; }

            public void Set(ExecutionContext context) => Current = context;

            public void Clear() => Current = null;
        }

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


        private sealed record ExecutionAttemptOutcome(
            AiExecutionRecord? Record,
            Exception? Exception);
    }
}