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
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Retention;
using Multiplexed.AI.Runtime.Retention.Decisions;
using Multiplexed.AI.Tests.Models;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Retention
{
    /// <summary>
    /// Unit tests for <see cref="AiExecutionRetentionService"/>.
    ///
    /// PURPOSE:
    /// - Validate retention application order without involving the DAG engine.
    /// - Ensure evicted steps are persisted before they are removed from the hot state.
    /// - Protect against data loss during retention.
    ///
    /// IMPORTANT:
    /// - These tests validate service orchestration only.
    /// - Policy selection is tested separately in AiExecutionRetentionPolicyTests.
    /// - Resolver behavior will be tested separately in AiExecutionStepResolverTests.
    /// </summary>
    public sealed class AiExecutionRetentionServiceTests
    {
        /// <summary>
        /// Verifies that an evicted step is saved before it is removed from the hot state.
        ///
        /// EXPECTATION:
        /// - RetentionService receives a plan containing one step to evict.
        /// - The step is persisted through IAiStepPayloadStore before removal.
        /// - The step is indexed before removal.
        /// - Only after successful persistence/indexing is the step removed from state.Steps.
        ///
        /// WHY THIS MATTERS:
        /// - Removing before persisting would permanently lose execution step data.
        /// - This protects replay, resolver lookup, and post-retention recovery.
        /// </summary>
        [Fact]
        public async Task ApplyAsync_Should_Save_And_Index_Step_Before_Removing_From_State()
        {
            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = new AiStepState
                    {
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                        Result = new AiStepResult
                        {
                            Success = true,
                            Data = new Dictionary<string, object?>
                            {
                                ["value"] = "test"
                            }
                        }
                    }
                }
            };

            var policyResolver = new TestRetentionPolicyResolver(
                new AiExecutionRetentionPlan
                {
                    StepsToEvict = new[] { "step-1" }
                });

            var callLog = new List<string>();

            var stepPayloadStore = new TestStepPayloadStore(callLog);
            var stepPayloadIndexStore = new TestStepPayloadIndexStore(callLog, state);
            var payloadCompactor = new TestPayloadCompactor(callLog);
            var metrics = new TestRetentionMetrics();
            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var service = new AiExecutionRetentionService(
                policyResolver,
                stepPayloadStore,
                stepPayloadIndexStore,
                payloadCompactor,
                metrics, decisionService);

            await service.ApplyAsync(
                state,
                AiExecutionRetentionMode.Evict,
                CancellationToken.None);

            Assert.Contains("save:step-1", callLog);
            Assert.Contains("index:step-1", callLog);

            var saveIndex = callLog.IndexOf("save:step-1");
            var indexIndex = callLog.IndexOf("index:step-1");

            Assert.True(saveIndex >= 0);
            Assert.True(indexIndex >= 0);

            Assert.True(
                saveIndex < indexIndex,
                "Step must be saved before it is indexed.");

            Assert.DoesNotContain("step-1", state.Steps.Keys);
        }

        /// <summary>
        /// Verifies that the service does not remove a step from the hot state
        /// when step persistence fails.
        ///
        /// EXPECTATION:
        /// - SaveStepAsync throws.
        /// - MarkArchivedAsync is not called.
        /// - The step remains in state.Steps.
        ///
        /// WHY THIS MATTERS:
        /// - If persistence fails and the step is removed anyway, the runtime loses
        ///   execution history permanently.
        /// </summary>
        [Fact]
        public async Task ApplyAsync_Should_Not_Remove_Step_When_Save_Fails()
        {
            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = new AiStepState
                    {
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                        Result = new AiStepResult
                        {
                            Success = true,
                            Data = new Dictionary<string, object?>
                            {
                                ["value"] = "test"
                            }
                        }
                    }
                }
            };

            var policyResolver = new TestRetentionPolicyResolver(
                new AiExecutionRetentionPlan
                {
                    StepsToEvict = new[] { "step-1" }
                });

            var callLog = new List<string>();

            var stepPayloadStore = new FailingStepPayloadStore(callLog);
            var stepPayloadIndexStore = new TestStepPayloadIndexStore(callLog, state);
            var payloadCompactor = new TestPayloadCompactor(callLog);
            var metrics = new TestRetentionMetrics();
            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var service = new AiExecutionRetentionService(
                policyResolver,
                stepPayloadStore,
                stepPayloadIndexStore,
                payloadCompactor,
                metrics, decisionService);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ApplyAsync(
                    state,
                    AiExecutionRetentionMode.Evict,
                    CancellationToken.None).AsTask());

            Assert.Contains("save-failed:step-1", callLog);
            Assert.DoesNotContain("index:step-1", callLog);

            Assert.True(
                state.Steps.ContainsKey("step-1"),
                "Step must remain in hot state when persistence fails.");
        }

        /// <summary>
        /// Verifies that the service does not remove a step from the hot state
        /// when archive indexing fails after the payload was saved.
        ///
        /// EXPECTATION:
        /// - SaveStepAsync succeeds.
        /// - MarkArchivedAsync throws.
        /// - The step remains in state.Steps.
        ///
        /// WHY THIS MATTERS:
        /// - If the step is removed after payload save but before archive index write,
        ///   the payload may exist but the resolver cannot find it.
        /// </summary>
        [Fact]
        public async Task ApplyAsync_Should_Not_Remove_Step_When_Index_Fails()
        {
            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = new AiStepState
                    {
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                        Result = new AiStepResult
                        {
                            Success = true,
                            Data = new Dictionary<string, object?>
                            {
                                ["value"] = "test"
                            }
                        }
                    }
                }
            };

            var policyResolver = new TestRetentionPolicyResolver(
                new AiExecutionRetentionPlan
                {
                    StepsToEvict = new[] { "step-1" }
                });

            var callLog = new List<string>();

            var stepPayloadStore = new TestStepPayloadStore(callLog);
            var stepPayloadIndexStore = new FailingStepPayloadIndexStore(callLog, state);
            var payloadCompactor = new TestPayloadCompactor(callLog);
            var metrics = new TestRetentionMetrics();

            var trigger = new TestExecutionRetentionTrigger(true);
            var decisionService = new DefaultAiExecutionRetentionDecisionService(trigger,
                new CompositeAiExecutionRetentionDecisionEvaluator(
                    Array.Empty<IAiExecutionRetentionDecisionPolicy>()));

            var service = new AiExecutionRetentionService(
                policyResolver,
                stepPayloadStore,
                stepPayloadIndexStore,
                payloadCompactor,
                metrics, decisionService);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ApplyAsync(
                    state,
                    AiExecutionRetentionMode.Evict,
                    CancellationToken.None).AsTask());

            Assert.Contains("save:step-1", callLog);
            Assert.Contains("index-failed:step-1", callLog);

            Assert.True(
                state.Steps.ContainsKey("step-1"),
                "Step must remain in hot state when archive indexing fails.");
        }

        /// <summary>
        /// Test policy resolver returning a fixed retention plan.
        /// </summary>
        private sealed class TestRetentionPolicyResolver : IAiExecutionRetentionPolicyResolver
        {
            private readonly IAiExecutionRetentionPolicy _policy;

            public TestRetentionPolicyResolver(AiExecutionRetentionPlan plan)
            {
                _policy = new TestRetentionPolicy(plan);
            }

            public IAiExecutionRetentionPolicy Resolve(AiExecutionRetentionMode mode)
            {
                return _policy;
            }
        }

        /// <summary>
        /// Test retention policy returning a predefined plan.
        /// </summary>
        private sealed class TestRetentionPolicy : IAiExecutionRetentionPolicy
        {
            private readonly AiExecutionRetentionPlan _plan;

            public TestRetentionPolicy(AiExecutionRetentionPlan plan)
            {
                _plan = plan;
            }

            public AiExecutionRetentionMode Mode => AiExecutionRetentionMode.Evict;

            public ValueTask<AiExecutionRetentionPlan> EvaluateAsync(
                AiExecutionState state,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<AiExecutionRetentionPlan>(_plan);
            }
        }

        /// <summary>
        /// Test step payload store that records save ordering.
        /// </summary>
        private sealed class TestStepPayloadStore : IAiStepPayloadStore
        {
            private readonly List<string> _callLog;

            public TestStepPayloadStore(List<string> callLog)
            {
                _callLog = callLog;
            }

            public Task SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                _callLog.Add($"save:{stepName}");
                return Task.CompletedTask;
            }

            public Task<AiStepState?> LoadStepAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AiStepState?>(null);
            }

            Task<AiStoredPayload> IAiStepPayloadStore.SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken)
            {
                _callLog.Add($"save:{stepName}");

                return Task.FromResult(new AiStoredPayload
                {
                    IsInline = true,
                    InlineValue = step.Result?.Data, // ou null si tu veux minimal
                    SizeBytes = 0,
                    ContentType = "application/json"
                });
            }

            public Task<AiStepState?> LoadStepAsync(
                string executionId,
                string stepName,
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AiStepState?>(null);
            }
        }

        /// <summary>
        /// Test step index store that records index ordering.
        /// </summary>
        private sealed class TestStepPayloadIndexStore : IAiStepPayloadIndexStore
        {
            private readonly List<string> _callLog;
            private readonly AiExecutionState _state;

            public TestStepPayloadIndexStore(
                List<string> callLog,
                AiExecutionState state)
            {
                _callLog = callLog;
                _state = state;
            }

            public Task MarkArchivedAsync(
                AiArchivedStepPayloadIndex entry,
                CancellationToken cancellationToken = default)
            {
                Assert.True(
                    _state.Steps.ContainsKey(entry.StepName),
                    "Step must still exist in hot state while archive index is written.");

                _callLog.Add($"index:{entry.StepName}");
                return Task.CompletedTask;
            }

            public Task DeleteAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<AiArchivedStepPayloadIndex?> GetAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AiArchivedStepPayloadIndex?>(null);
            }

            public Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetByExecutionAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AiArchivedStepPayloadIndex>>(
                    new List<AiArchivedStepPayloadIndex>());
            }

            public Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
                string executionId,
                IReadOnlyCollection<string> stepNames,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>>(
                    new Dictionary<string, AiArchivedStepPayloadIndex>());
            }

            public Task IndexStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Test payload compactor.
        /// </summary>
        private sealed class TestPayloadCompactor : IAiStepResultPayloadCompactor
        {
            private readonly List<string> _callLog;

            public TestPayloadCompactor(List<string> callLog)
            {
                _callLog = callLog;
            }

            public Task CompactAsync(
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                _callLog.Add($"compact:{step.StepName}");
                return Task.CompletedTask;
            }

            public Task CompactAsync(AiStepResult result, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Minimal test metrics implementation.
        /// </summary>
        private sealed class TestRetentionMetrics : IAiExecutionRetentionServiceMetrics
        {
            public void RecordCompacted(string stepName)
            {
            }

            public void RecordCompleted(AiExecutionRetentionMode mode, int totalStepsBefore, int totalStepsAfter)
            {
            }

            public void RecordEvaluation(AiExecutionRetentionMode mode, int totalStepsBefore, int stepsToCompact, int stepsToEvict)
            {
            }

            public void RecordEvicted(string stepName)
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
        /// Step payload store that simulates a persistence failure.
        /// </summary>
        private sealed class FailingStepPayloadStore : IAiStepPayloadStore
        {
            private readonly List<string> _callLog;

            public FailingStepPayloadStore(List<string> callLog)
            {
                _callLog = callLog;
            }

            Task<AiStoredPayload> IAiStepPayloadStore.SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken)
            {
                _callLog.Add($"save-failed:{stepName}");

                throw new InvalidOperationException("Simulated step persistence failure.");
            }

            public Task<AiStepState?> LoadStepAsync(
                string executionId,
                string stepName,
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AiStepState?>(null);
            }
        }

        /// <summary>
        /// Archive index store that simulates an indexing failure.
        /// </summary>
        private sealed class FailingStepPayloadIndexStore : IAiStepPayloadIndexStore
        {
            private readonly List<string> _callLog;
            private readonly AiExecutionState _state;

            public FailingStepPayloadIndexStore(
                List<string> callLog,
                AiExecutionState state)
            {
                _callLog = callLog;
                _state = state;
            }

            public Task MarkArchivedAsync(
                AiArchivedStepPayloadIndex entry,
                CancellationToken cancellationToken = default)
            {
                Assert.True(
                    _state.Steps.ContainsKey(entry.StepName),
                    "Step must still exist in hot state while archive index write is attempted.");

                _callLog.Add($"index-failed:{entry.StepName}");

                throw new InvalidOperationException("Simulated archive index failure.");
            }

            public Task DeleteAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<AiArchivedStepPayloadIndex?> GetAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AiArchivedStepPayloadIndex?>(null);
            }

            public Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetByExecutionAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AiArchivedStepPayloadIndex>>(
                    new List<AiArchivedStepPayloadIndex>());
            }

            public Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
                string executionId,
                IReadOnlyCollection<string> stepNames,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>>(
                    new Dictionary<string, AiArchivedStepPayloadIndex>());
            }

            public Task IndexStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}