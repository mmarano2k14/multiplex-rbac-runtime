using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Retention.Services;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Retention
{
    /// <summary>
    /// Unit tests for the policy-driven <see cref="DefaultAiRetentionEvictionService"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate physical eviction ordering independently from the DAG runtime.
    /// - Ensure archived payload persistence happens before hot-state removal.
    /// - Protect against data loss during retention eviction.
    ///
    /// IMPORTANT:
    /// - These tests intentionally validate only eviction orchestration behavior.
    /// - Policy selection and retention planning are validated separately.
    /// - These tests target the simplified policy-driven eviction API.
    /// </remarks>
    public sealed class AiPolicyDrivenRetentionEvictionServiceTests
    {
        /// <summary>
        /// Verifies that a step payload is persisted and indexed before removal from hot state.
        /// </summary>
        [Fact]
        public async Task EvictAsync_Should_Save_And_Index_Step_Before_Removing_From_State()
        {
            var state = CreateState();

            var callLog = new List<string>();

            var stepPayloadStore = new TestStepPayloadStore(callLog);
            var stepPayloadIndexStore = new TestStepPayloadIndexStore(callLog, state);

            var service = new DefaultAiRetentionEvictionService(
                stepPayloadStore,
                stepPayloadIndexStore);

            var result = await service.EvictAsync(
                state,
                new[] { "step-1" },
                "unit-test",
                CancellationToken.None);

            Assert.NotNull(result);

            Assert.Contains("save:step-1", callLog);
            Assert.Contains("index:step-1", callLog);

            var saveIndex = callLog.IndexOf("save:step-1");
            var indexIndex = callLog.IndexOf("index:step-1");

            Assert.True(saveIndex >= 0);
            Assert.True(indexIndex >= 0);

            Assert.True(
                saveIndex < indexIndex,
                "Step payload must be saved before archive indexing.");

          

            Assert.Single(result);
            Assert.Contains("step-1", result);
        }

        /// <summary>
        /// Verifies that a step remains in hot state when payload persistence fails.
        /// </summary>
        [Fact]
        public async Task EvictAsync_Should_Not_Remove_Step_When_Save_Fails()
        {
            var state = CreateState();

            var callLog = new List<string>();

            var stepPayloadStore = new FailingStepPayloadStore(callLog);
            var stepPayloadIndexStore = new TestStepPayloadIndexStore(callLog, state);

            var service = new DefaultAiRetentionEvictionService(
                stepPayloadStore,
                stepPayloadIndexStore);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.EvictAsync(
                    state,
                    new[] { "step-1" },
                    "unit-test",
                    CancellationToken.None));

            Assert.Contains("save-failed:step-1", callLog);

            Assert.DoesNotContain("index:step-1", callLog);

            Assert.True(
                state.Steps.ContainsKey("step-1"),
                "Step must remain in hot state when payload persistence fails.");
        }

        /// <summary>
        /// Verifies that a step remains in hot state when archive indexing fails.
        /// </summary>
        [Fact]
        public async Task EvictAsync_Should_Not_Remove_Step_When_Index_Fails()
        {
            var state = CreateState();

            var callLog = new List<string>();

            var stepPayloadStore = new TestStepPayloadStore(callLog);
            var stepPayloadIndexStore = new FailingStepPayloadIndexStore(callLog, state);

            var service = new DefaultAiRetentionEvictionService(
                stepPayloadStore,
                stepPayloadIndexStore);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.EvictAsync(
                    state,
                    new[] { "step-1" },
                    "unit-test",
                    CancellationToken.None));

            Assert.Contains("save:step-1", callLog);
            Assert.Contains("index-failed:step-1", callLog);

            Assert.True(
                state.Steps.ContainsKey("step-1"),
                "Step must remain in hot state when archive indexing fails.");
        }

        /// <summary>
        /// Creates a deterministic execution state for eviction validation.
        /// </summary>
        private static AiExecutionState CreateState()
        {
            return new AiExecutionState
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
        }

        /// <summary>
        /// Test payload store recording save ordering.
        /// </summary>
        private sealed class TestStepPayloadStore : IAiStepPayloadStore
        {
            private readonly List<string> _callLog;

            public TestStepPayloadStore(List<string> callLog)
            {
                _callLog = callLog;
            }

            public Task<AiStoredPayload> SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                _callLog.Add($"save:{stepName}");

                return Task.FromResult(new AiStoredPayload
                {
                    IsInline = true,
                    InlineValue = step.Result?.Data,
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
        /// Test archive index store recording archive ordering.
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
        }

        /// <summary>
        /// Payload store simulating persistence failure.
        /// </summary>
        private sealed class FailingStepPayloadStore : IAiStepPayloadStore
        {
            private readonly List<string> _callLog;

            public FailingStepPayloadStore(List<string> callLog)
            {
                _callLog = callLog;
            }

            public Task<AiStoredPayload> SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                _callLog.Add($"save-failed:{stepName}");

                throw new InvalidOperationException(
                    "Simulated step payload persistence failure.");
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
        /// Archive index store simulating archive write failure.
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
                    "Step must still exist in hot state while archive indexing is attempted.");

                _callLog.Add($"index-failed:{entry.StepName}");

                throw new InvalidOperationException(
                    "Simulated archive index write failure.");
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
        }
    }
}
