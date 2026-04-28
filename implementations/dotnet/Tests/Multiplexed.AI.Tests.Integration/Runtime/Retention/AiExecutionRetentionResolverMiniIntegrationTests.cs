using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;
using Multiplexed.Abstractions.AI.Execution.Retention.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Retention;
using Multiplexed.AI.Runtime.Retention.Decisions;
using Multiplexed.AI.Runtime.Retention.Policies;
using Multiplexed.AI.Tests.Models;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Retention
{
    /// <summary>
    /// Small integration tests for the retention + resolver boundary.
    ///
    /// PURPOSE:
    /// - Validate the RetentionService and DefaultAiExecutionStepResolver together.
    /// - Prove that an evicted step is still visible through the archive index.
    /// - Prove that status lookup remains lazy and does not load the full archived payload.
    /// - Prove that full step lookup restores the archived step on demand.
    ///
    /// IMPORTANT:
    /// - This test intentionally avoids the DAG engine.
    /// - This test intentionally avoids Redis and MongoDB.
    /// - This is not a stress test; it is a small deterministic integration test
    ///   for the retention/resolver contract.
    /// </summary>
    public sealed class AiExecutionRetentionResolverMiniIntegrationTests
    {
        /// <summary>
        /// Verifies the full retention + resolver flow without running the DAG engine.
        ///
        /// SCENARIO:
        /// - A state contains six completed steps.
        /// - Retention in Evict mode keeps only three hot steps.
        /// - Evicted steps are saved to the step payload store and indexed as archived.
        /// - The resolver can read archived status without loading the payload.
        /// - The resolver can load the full archived step when explicitly requested.
        ///
        /// EXPECTATION:
        /// - Hot state is reduced from six steps to three steps.
        /// - Three steps are reported as evicted.
        /// - GetStepStatusAsync returns the archived status without payload load.
        /// - GetStepAsync loads the archived step payload exactly once.
        ///
        /// WHY THIS MATTERS:
        /// - This proves retention does not make evicted steps invisible.
        /// - This protects DAG selector/convergence behavior after eviction.
        /// - This keeps the proof small and deterministic, unlike large DAG retention stress tests.
        /// </summary>
        [Fact]
        public async Task Retention_Should_Evict_Step_And_Resolver_Should_Resolve_Archived_Step()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Evict,
                    MaxCompletedStepsInState = 3
                }
            });

            var state = CreateCompletedState(6);

            var policyResolver = new TestRetentionPolicyResolver(
                new EvictAiExecutionRetentionPolicy(options));

            var payloadStore = new InMemoryStepPayloadStore();
            var indexStore = new InMemoryStepPayloadIndexStore();
            var compactor = new NoopStepResultPayloadCompactor();
            var metrics = new NoopRetentionMetrics();

            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var retentionService = new AiExecutionRetentionService(
                policyResolver,
                payloadStore,
                indexStore,
                compactor,
                metrics, decisionService);

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore
                );

            var result = await retentionService.ApplyAsync(
                state,
                AiExecutionRetentionMode.Evict,
                CancellationToken.None);

            Assert.Equal(3, state.Steps.Count);
            Assert.Equal(3, result.EvictedSteps.Count);

            var evictedStepName = result.EvictedSteps.First();

            Assert.False(
                state.Steps.ContainsKey(evictedStepName),
                "Evicted step must no longer exist in the hot state.");

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
            Assert.NotNull(fullStep.Result);
            Assert.NotNull(fullStep.Result!.Data);
            Assert.Equal($"value-{evictedStepName}", fullStep.Result.Data["value"]);

            Assert.Equal(
                1,
                payloadStore.LoadStepAsyncCallCount);
        }

        /// <summary>
        /// Verifies that retention does not evict a completed parent step that is still
        /// required by a non-terminal child.
        ///
        /// SCENARIO:
        /// - "parent" is completed.
        /// - "child" is Ready and depends on "parent".
        /// - "independent-old" is completed and has no active dependent.
        /// - Evict mode is above the configured threshold.
        ///
        /// EXPECTATION:
        /// - "parent" remains in the hot state.
        /// - "independent-old" is evicted and archived.
        /// - Resolver can still resolve the archived independent step.
        ///
        /// WHY THIS MATTERS:
        /// - This validates the dependency-safe eviction rule through RetentionService,
        ///   not only at policy level.
        /// - It protects the DAG engine from loops caused by missing dependencies.
        /// </summary>
        [Fact]
        public async Task Retention_Should_Not_Evict_Completed_Parent_Required_By_NonTerminal_Child()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Evict,
                    MaxCompletedStepsInState = 1
                }
            });

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["parent"] = new AiStepState
                    {
                        StepName = "parent",
                        Status = AiStepExecutionStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-3),
                        Result = new AiStepResult
                        {
                            Success = true,
                            Data = new Dictionary<string, object?>
                            {
                                ["value"] = "parent-value"
                            }
                        }
                    },

                    ["independent-old"] = new AiStepState
                    {
                        StepName = "independent-old",
                        Status = AiStepExecutionStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                        Result = new AiStepResult
                        {
                            Success = true,
                            Data = new Dictionary<string, object?>
                            {
                                ["value"] = "independent-value"
                            }
                        }
                    },

                    ["child"] = new AiStepState
                    {
                        StepName = "child",
                        Status = AiStepExecutionStatus.Ready,
                        DependsOn = new List<string> { "parent" }
                    }
                }
            };

            var policyResolver = new TestRetentionPolicyResolver(
                new EvictAiExecutionRetentionPolicy(options));

            var payloadStore = new InMemoryStepPayloadStore();
            var indexStore = new InMemoryStepPayloadIndexStore();
            var compactor = new NoopStepResultPayloadCompactor();
            var metrics = new NoopRetentionMetrics();

            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var retentionService = new AiExecutionRetentionService(
                policyResolver,
                payloadStore,
                indexStore,
                compactor,
                metrics, decisionService);

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore
                );

            var result = await retentionService.ApplyAsync(
                state,
                AiExecutionRetentionMode.Evict,
                CancellationToken.None);

            Assert.Contains("parent", state.Steps.Keys);
            Assert.Contains("child", state.Steps.Keys);

            Assert.DoesNotContain("parent", result.EvictedSteps);
            Assert.Contains("independent-old", result.EvictedSteps);
            Assert.DoesNotContain("independent-old", state.Steps.Keys);

            var archived = await resolver.GetStepAsync(
                state.ExecutionId,
                "independent-old",
                state,
                CancellationToken.None);

            Assert.NotNull(archived);
            Assert.Equal("independent-old", archived!.StepName);
            Assert.Equal("independent-value", archived.Result!.Data!["value"]);
        }

        /// <summary>
        /// Verifies that Hybrid retention evicts overflow steps and compacts remaining terminal steps.
        ///
        /// SCENARIO:
        /// - State contains three completed steps.
        /// - MaxCompletedStepsInState is two.
        /// - Hybrid mode should evict the oldest step and compact the two remaining steps.
        ///
        /// EXPECTATION:
        /// - One step is evicted and archived.
        /// - Two remaining hot steps are compacted.
        /// - The evicted step is still resolvable through the resolver.
        /// - The evicted step is not also counted as compacted.
        ///
        /// WHY THIS MATTERS:
        /// - Validates the corrected Hybrid behavior through RetentionService.
        /// - Ensures compact and evict remain separate operations.
        /// </summary>
        /// <summary>
        /// Verifies that Hybrid retention evicts overflow steps and compacts remaining terminal steps.
        ///
        /// SCENARIO:
        /// - State contains three completed steps.
        /// - MaxCompletedStepsInState is two.
        /// - Hybrid mode should evict the oldest step and compact the two remaining steps.
        ///
        /// EXPECTATION:
        /// - One step is evicted and archived.
        /// - Two remaining hot steps are compacted.
        /// - The evicted step is still resolvable through the resolver.
        /// - The evicted step is not also counted as compacted.
        /// - Compaction is executed via AiStepResult (not AiStepState).
        ///
        /// WHY THIS MATTERS:
        /// - Validates the corrected Hybrid behavior through RetentionService.
        /// - Ensures compact and evict remain separate operations.
        /// </summary>
        [Fact]
        public async Task HybridRetention_Should_Compact_Remaining_Steps_And_Evict_Overflow()
        {
            // Arrange
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Hybrid,
                    MaxCompletedStepsInState = 2
                }
            });

            var state = CreateCompletedState(3);

            var policyResolver = new TestRetentionPolicyResolver(
                new HybridAiExecutionRetentionPolicy(options));

            var payloadStore = new InMemoryStepPayloadStore();
            var indexStore = new InMemoryStepPayloadIndexStore();
            var compactor = new TrackingStepResultPayloadCompactor();
            var metrics = new NoopRetentionMetrics();

            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var retentionService = new AiExecutionRetentionService(
                policyResolver,
                payloadStore,
                indexStore,
                compactor,
                metrics, decisionService);

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore
                );

            // Act
            var result = await retentionService.ApplyAsync(
                state,
                AiExecutionRetentionMode.Hybrid,
                CancellationToken.None);

            // Assert — Eviction
            Assert.Single(result.EvictedSteps);
            Assert.Contains("step-00", result.EvictedSteps);

            // Assert — Compaction (via result, NOT via compactor fake)
            Assert.Equal(2, result.CompactedSteps.Count);
            Assert.Contains("step-01", result.CompactedSteps);
            Assert.Contains("step-02", result.CompactedSteps);

            // Assert — No overlap
            Assert.DoesNotContain(
                result.EvictedSteps,
                stepName => result.CompactedSteps.Contains(stepName));

            // Assert — State correctness
            Assert.DoesNotContain("step-00", state.Steps.Keys);
            Assert.Contains("step-01", state.Steps.Keys);
            Assert.Contains("step-02", state.Steps.Keys);

            // Assert — Compactor was called twice (via Result)
            Assert.Equal(2, compactor.CompactResultCallCount);

            // Assert — Resolver still works for evicted step
            var archived = await resolver.GetStepAsync(
                state.ExecutionId,
                "step-00",
                state,
                CancellationToken.None);

            Assert.NotNull(archived);
            Assert.Equal("step-00", archived!.StepName);
            Assert.Equal("value-step-00", archived.Result!.Data!["value"]);
        }

        /// <summary>
        /// Verifies that Hybrid retention does not evict any step
        /// when all terminal steps are still required by non-terminal children.
        ///
        /// SCENARIO:
        /// - Multiple completed steps exist.
        /// - Each completed step is required by at least one non-terminal child.
        /// - MaxCompletedStepsInState is lower than total steps.
        ///
        /// EXPECTATION:
        /// - No step is evicted.
        /// - Steps remain in hot state.
        /// - No data loss.
        /// - Compaction may still occur.
        ///
        /// WHY THIS MATTERS:
        /// - Prevents breaking DAG execution by removing required dependencies.
        /// - Validates dependency-safe retention in Hybrid mode.
        /// </summary>
        [Fact]
        public async Task Hybrid_Should_Not_Evict_When_All_Steps_Are_Protected_By_Dependencies()
        {
            // Arrange
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Hybrid,
                    MaxCompletedStepsInState = 1 // force overflow
                }
            });

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-a"] = new AiStepState
                    {
                        StepName = "step-a",
                        Status = AiStepExecutionStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-3),
                        Result = CreateResult("A")
                    },
                    ["step-b"] = new AiStepState
                    {
                        StepName = "step-b",
                        Status = AiStepExecutionStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                        Result = CreateResult("B")
                    },
                    ["child-a"] = new AiStepState
                    {
                        StepName = "child-a",
                        Status = AiStepExecutionStatus.Ready,
                        DependsOn = new List<string> { "step-a" }
                    },
                    ["child-b"] = new AiStepState
                    {
                        StepName = "child-b",
                        Status = AiStepExecutionStatus.Ready,
                        DependsOn = new List<string> { "step-b" }
                    }
                }
            };

            var policyResolver = new TestRetentionPolicyResolver(
                new HybridAiExecutionRetentionPolicy(options));

            var payloadStore = new InMemoryStepPayloadStore();
            var indexStore = new InMemoryStepPayloadIndexStore();
            var compactor = new TrackingStepResultPayloadCompactor();
            var metrics = new NoopRetentionMetrics();

            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var retentionService = new AiExecutionRetentionService(
                policyResolver,
                payloadStore,
                indexStore,
                compactor,
                metrics, decisionService);

            // Act
            var result = await retentionService.ApplyAsync(
                state,
                AiExecutionRetentionMode.Hybrid,
                CancellationToken.None);

            // Assert — No eviction
            Assert.Empty(result.EvictedSteps);

            // Assert — All steps still in state
            Assert.Contains("step-a", state.Steps.Keys);
            Assert.Contains("step-b", state.Steps.Keys);

            // Assert — Compaction still possible
            Assert.Contains("step-a", result.CompactedSteps);
            Assert.Contains("step-b", result.CompactedSteps);

            // Assert — Compactor called twice
            Assert.Equal(2, compactor.CompactResultCallCount);
        }

        private static AiStepResult CreateResult(string value)
        {
            return new AiStepResult
            {
                Success = true,
                Data = new Dictionary<string, object?>
                {
                    ["value"] = value
                }
            };
        }

        /// <summary>
        /// Creates a deterministic completed execution state.
        /// Older steps have earlier CompletedAtUtc values and are evicted first.
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
        /// Test resolver that always returns one concrete retention policy.
        /// </summary>
        private sealed class TestRetentionPolicyResolver : IAiExecutionRetentionPolicyResolver
        {
            private readonly IAiExecutionRetentionPolicy _policy;

            public TestRetentionPolicyResolver(IAiExecutionRetentionPolicy policy)
            {
                _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            }

            public IAiExecutionRetentionPolicy Resolve(AiExecutionRetentionMode mode)
            {
                return _policy;
            }
        }

        /// <summary>
        /// In-memory archived step payload store used by the mini integration test.
        ///
        /// PURPOSE:
        /// - Save full evicted steps.
        /// - Restore full evicted steps on explicit resolver lookup.
        /// - Count payload loads to prove lazy status resolution.
        /// </summary>
        private sealed class InMemoryStepPayloadStore : IAiStepPayloadStore
        {
            private readonly Dictionary<string, AiStepState> _steps = new();

            public int LoadStepAsyncCallCount { get; private set; }

            Task<AiStoredPayload> IAiStepPayloadStore.SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken)
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

            private static string BuildKey(string executionId, string stepName)
            {
                return $"{executionId}:{stepName}";
            }

            private static AiStepState CloneStep(AiStepState step)
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
        /// In-memory archived step index store used by the mini integration test.
        ///
        /// PURPOSE:
        /// - Store archive metadata produced by RetentionService.
        /// - Provide single and batch archive index lookup for the resolver.
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
                var result = _entries
                    .Values
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

            public Task IndexStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            private static string BuildKey(string executionId, string stepName)
            {
                return $"{executionId}:{stepName}";
            }
        }

        /// <summary>
        /// No-op compactor because this mini integration test exercises Evict mode only.
        /// </summary>
        private sealed class NoopStepResultPayloadCompactor : IAiStepResultPayloadCompactor
        {
            public Task CompactAsync(
                AiStepResult result,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task CompactAsync(
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// No-op retention metrics implementation.
        ///
        /// PURPOSE:
        /// - Keep this test focused on retention/resolver behavior.
        /// - Metrics behavior is tested separately.
        /// </summary>
        private sealed class NoopRetentionMetrics : IAiExecutionRetentionServiceMetrics
        {
            public void RecordEvaluation(
                AiExecutionRetentionMode mode,
                int totalStepsBefore,
                int stepsToCompact,
                int stepsToEvict)
            {
            }

            public void RecordCompacted(string stepName)
            {
            }

            public void RecordEvicted(string stepName)
            {
            }

            public void RecordCompleted(
                AiExecutionRetentionMode mode,
                int totalStepsBefore,
                int totalStepsAfter)
            {
            }

            public void RecordRetentionApplied(
                AiExecutionRetentionMode mode,
                int totalStepsBefore,
                int totalStepsAfter,
                int plannedCompactions,
                int plannedEvictions,
                int compactedSteps,
                int evictedSteps)
            {
            }
        }


        /// <summary>
        /// Tracking compactor used to verify which steps were compacted by Hybrid retention.
        /// </summary>
        /// <summary>
        /// Tracking compactor aligned with RetentionService behavior.
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
