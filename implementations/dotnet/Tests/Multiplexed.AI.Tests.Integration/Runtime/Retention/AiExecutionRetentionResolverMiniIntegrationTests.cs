using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Retention.Models;
using Multiplexed.AI.Runtime.Execution.Retention.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Services;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Retention
{
    /// <summary>
    /// Small integration tests validating the retention + resolver boundary.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate retention engine orchestration together with the step resolver.
    /// - Validate archive index visibility after eviction.
    /// - Validate lazy archived status resolution.
    /// - Validate archived step payload restoration on demand.
    ///
    /// IMPORTANT:
    /// - These tests intentionally avoid the DAG engine.
    /// - These tests intentionally avoid Redis and MongoDB.
    /// - Retention is fully policy-driven and config-driven.
    /// - Legacy retention modes/options/services are no longer used.
    /// </remarks>
    public sealed class AiPolicyDrivenRetentionResolverMiniIntegrationTests
    {
        /// <summary>
        /// Verifies that evicted steps remain resolvable through the archive index.
        /// </summary>
        [Fact]
        public async Task Retention_Should_Evict_Step_And_Resolver_Should_Resolve_Archived_Step()
        {
            var state = CreateCompletedState(6);

            var payloadStore = new InMemoryStepPayloadStore();
            var indexStore = new InMemoryStepPayloadIndexStore();

            var evictionService = new DefaultAiRetentionEvictionService(
                payloadStore,
                indexStore);

            var policy = new EvictAiRetentionPolicy();

            var context = new AiRetentionContext
            {
                ExecutionState = state
            };

            var policyResult = await policy.ExecuteAsync(context);

            var decision = GetDecision(policyResult);

            var stepsToEvict = decision.StepsToEvict
                .Take(3)
                .ToArray();

            var evictedSteps = await evictionService.EvictAsync(
                state,
                stepsToEvict,
                "integration-test");

            foreach (var stepName in stepsToEvict)
            {
                Assert.DoesNotContain(stepName, state.Steps.Keys);
            }

            Assert.Equal(3, evictedSteps.Count);

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore);

            var evictedStepName = evictedSteps.First();

            var archivedIndex = await indexStore.GetAsync(
                state.ExecutionId,
                evictedStepName,
                CancellationToken.None);

            Assert.NotNull(archivedIndex);
            Assert.Equal(AiStepExecutionStatus.Completed, archivedIndex!.Status);

            var statusStep = await resolver.GetStepStatusAsync(
                state.ExecutionId,
                evictedStepName,
                state,
                CancellationToken.None);

            Assert.NotNull(statusStep);
            Assert.Equal(evictedStepName, statusStep!.StepName);
            Assert.Equal(AiStepExecutionStatus.Completed, statusStep.Status);

            Assert.Equal(
                0,
                payloadStore.LoadStepAsyncCallCount);

            var fullStep = await resolver.GetStepAsync(
                state.ExecutionId,
                evictedStepName,
                state,
                CancellationToken.None);

            Assert.NotNull(fullStep);
            Assert.Equal(evictedStepName, fullStep!.StepName);
            Assert.Equal(AiStepExecutionStatus.Completed, fullStep.Status);

            Assert.Equal(
                1,
                payloadStore.LoadStepAsyncCallCount);
        }

        /// <summary>
        /// Verifies that Hybrid retention compacts remaining hot steps and evicts overflow steps.
        /// </summary>
        [Fact]
        public async Task HybridRetention_Should_Compact_Remaining_Steps_And_Evict_Overflow()
        {
            var state = CreateCompletedState(3);

            var payloadStore = new InMemoryStepPayloadStore();
            var indexStore = new InMemoryStepPayloadIndexStore();

            var evictionService = new DefaultAiRetentionEvictionService(
                payloadStore,
                indexStore);

            var compactor = new TrackingStepResultPayloadCompactor();

            var policy = new HybridAiRetentionPolicy();

            var context = new AiRetentionContext
            {
                ExecutionState = state
            };

            var policyResult = await policy.ExecuteAsync(context);

            var decision = GetDecision(policyResult);

            var stepsToEvict = decision.StepsToEvict
                .Take(1)
                .ToArray();

            var stepsToCompact = decision.StepsToCompact
                .Skip(1)
                .ToArray();

            foreach (var stepName in stepsToCompact)
            {
                var step = state.Steps[stepName];

                Assert.NotNull(step.Result);

                await compactor.CompactAsync(step.Result!);
            }

            var evictedSteps = await evictionService.EvictAsync(
                state,
                stepsToEvict,
                "hybrid-test");

            Assert.Single(evictedSteps);

            Assert.Equal(2, compactor.CompactResultCallCount);

            Assert.DoesNotContain(
                evictedSteps.First(),
                stepsToCompact);

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore);

            var archived = await resolver.GetStepAsync(
                state.ExecutionId,
                evictedSteps.First(),
                state,
                CancellationToken.None);

            Assert.NotNull(archived);
        }

        /// <summary>
        /// Extracts a strongly typed retention decision from a policy result.
        /// </summary>
        private static AiRetentionDecision GetDecision(
            AiPolicyResult result)
        {
            var typed = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result);

            Assert.NotNull(typed.Data);

            return typed.Data!;
        }

        /// <summary>
        /// Creates a deterministic completed execution state.
        /// </summary>
        private static AiExecutionState CreateCompletedState(int count)
        {
            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>()
            };

            for (var i = 0; i < count; i++)
            {
                var stepName = $"step-{i:D2}";

                state.Steps[stepName] = new AiStepState
                {
                    StepName = stepName,
                    Status = AiStepExecutionStatus.Completed,
                    CompletedAtUtc = DateTime.UtcNow.AddMinutes(-count + i),
                    Result = new AiStepResult
                    {
                        Success = true,
                        Data = new Dictionary<string, object?>
                        {
                            ["value"] = $"value-{stepName}"
                        }
                    }
                };
            }

            return state;
        }

        /// <summary>
        /// In-memory archived step payload store used by the integration tests.
        /// </summary>
        private sealed class InMemoryStepPayloadStore : IAiStepPayloadStore
        {
            private readonly Dictionary<string, AiStepState> _steps = new();

            public int LoadStepAsyncCallCount { get; private set; }

            public Task<AiStoredPayload> SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                var key = BuildKey(executionId, stepName);

                _steps[key] = CloneStep(step);

                return Task.FromResult(new AiStoredPayload
                {
                    IsInline = true,
                    InlineValue = key,
                    SizeBytes = 0,
                    ContentType = "application/x-ai-step-key"
                });
            }

            public Task<AiStepState?> LoadStepAsync(
                string executionId,
                string stepName,
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                LoadStepAsyncCallCount++;

                var key = payload.InlineValue as string
                    ?? BuildKey(executionId, stepName);

                _steps.TryGetValue(key, out var step);

                return Task.FromResult(step is null ? null : CloneStep(step));
            }

            private static string BuildKey(
                string executionId,
                string stepName)
            {
                return $"{executionId}:{stepName}";
            }

            private static AiStepState CloneStep(
                AiStepState step)
            {
                return new AiStepState
                {
                    StepName = step.StepName,
                    Status = step.Status,
                    CompletedAtUtc = step.CompletedAtUtc,
                    StartedAtUtc = step.StartedAtUtc,
                    UpdatedAtUtc = step.UpdatedAtUtc,
                    DependsOn = step.DependsOn is null
                        ? null
                        : new List<string>(step.DependsOn),
                    Result = step.Result is null
                        ? null
                        : new AiStepResult
                        {
                            Success = step.Result.Success,
                            Error = step.Result.Error,
                            Value = step.Result.Value,
                            Data = step.Result.Data is null
                                ? null
                                : new Dictionary<string, object?>(step.Result.Data),
                            Payload = step.Result.Payload,
                            DataPayloads = step.Result.DataPayloads
                        }
                };
            }
        }

        /// <summary>
        /// In-memory archived step index store.
        /// </summary>
        private sealed class InMemoryStepPayloadIndexStore : IAiStepPayloadIndexStore
        {
            private readonly Dictionary<string, AiArchivedStepPayloadIndex> _entries = new();

            public Task MarkArchivedAsync(
                AiArchivedStepPayloadIndex entry,
                CancellationToken cancellationToken = default)
            {
                _entries[BuildKey(entry.ExecutionId, entry.StepName)] = entry;

                return Task.CompletedTask;
            }

            public Task<AiArchivedStepPayloadIndex?> GetAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                _entries.TryGetValue(BuildKey(executionId, stepName), out var entry);

                return Task.FromResult(entry);
            }

            public Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
                string executionId,
                IReadOnlyCollection<string> stepNames,
                CancellationToken cancellationToken = default)
            {
                var result = stepNames
                    .Select(stepName =>
                    {
                        _entries.TryGetValue(BuildKey(executionId, stepName), out var entry);

                        return new { stepName, entry };
                    })
                    .Where(x => x.entry is not null)
                    .ToDictionary(x => x.stepName, x => x.entry!);

                return Task.FromResult<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>>(result);
            }

            public Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetByExecutionAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                var result = _entries.Values
                    .Where(x => string.Equals(x.ExecutionId, executionId, StringComparison.Ordinal))
                    .ToList();

                return Task.FromResult<IReadOnlyList<AiArchivedStepPayloadIndex>>(result);
            }

            public Task DeleteAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                _entries.Remove(BuildKey(executionId, stepName));

                return Task.CompletedTask;
            }

            private static string BuildKey(
                string executionId,
                string stepName)
            {
                return $"{executionId}:{stepName}";
            }
        }

        /// <summary>
        /// Tracking compactor used to validate Hybrid retention behavior.
        /// </summary>
        private sealed class TrackingStepResultPayloadCompactor : IAiStepResultPayloadCompactor
        {
            public int CompactResultCallCount { get; private set; }

            public Task CompactAsync(
                AiStepResult result,
                CancellationToken cancellationToken = default)
            {
                CompactResultCallCount++;

                return Task.CompletedTask;
            }

            public Task CompactAsync(
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}
