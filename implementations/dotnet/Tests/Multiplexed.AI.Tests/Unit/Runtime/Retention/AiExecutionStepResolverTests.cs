using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Retention
{
    /// <summary>
    /// Unit tests for the execution step resolver.
    ///
    /// PURPOSE:
    /// - Validate lazy resolution behavior after retention eviction.
    /// - Ensure status lookup uses the archive index only.
    /// - Ensure full archived step payloads are loaded only when explicitly requested.
    ///
    /// IMPORTANT:
    /// - These tests must not involve the DAG engine.
    /// - These tests must not involve MongoDB or Redis.
    /// - Payload loading must remain lazy to avoid memory regressions.
    /// </summary>
    public sealed class AiExecutionStepResolverTests
    {
        /// <summary>
        /// Verifies that reading only a step status does not load the full archived step payload.
        ///
        /// SCENARIO:
        /// - The step no longer exists in state.Steps because retention evicted it.
        /// - The archive index contains metadata for that step.
        /// - The caller only asks for the step status.
        ///
        /// EXPECTATION:
        /// - The resolver reads the archive index.
        /// - The resolver returns the archived status.
        /// - The resolver does NOT call IAiStepPayloadStore.LoadStepAsync.
        ///
        /// WHY THIS MATTERS:
        /// - Status checks are frequent in DAG execution.
        /// - Loading full payloads for status-only checks would destroy performance.
        /// - This protects the lazy resolver design.
        /// </summary>
        [Fact]
        public async Task GetStepStatusAsync_Should_Not_Load_Full_Payload_For_Evicted_Step()
        {
            // Arrange
            var state = new AiExecutionState
            {
                ExecutionId = "execution-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            var archivedPayload = new AiStoredPayload
            {
                IsInline = true,
                InlineValue = new Dictionary<string, object?>
                {
                    ["value"] = "archived"
                },
                SizeBytes = 0,
                ContentType = "application/json"
            };

            var indexStore = new TestStepPayloadIndexStore(
                new Dictionary<string, AiArchivedStepPayloadIndex>
                {
                    ["step-1"] = new AiArchivedStepPayloadIndex
                    {
                        ExecutionId = state.ExecutionId,
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Completed,
                        Payload = archivedPayload,
                        ArchivedAtUtc = DateTime.UtcNow,
                        Reason = "retention"
                    }
                });

            var payloadStore = new TestStepPayloadStore();

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore);

            // Act
            var step = await resolver.GetStepStatusAsync(
                state.ExecutionId,
                "step-1",
                state,
                CancellationToken.None);

            // Assert
            Assert.NotNull(step);
            Assert.Equal(AiStepExecutionStatus.Completed, step!.Status);

            Assert.Equal(
                1,
                indexStore.GetAsyncCallCount);

            Assert.Equal(
                0,
                payloadStore.LoadStepAsyncCallCount);
        }

        /// <summary>
        /// Verifies that resolving a full evicted step loads the archived payload.
        ///
        /// SCENARIO:
        /// - The step is no longer present in state.Steps.
        /// - The archive index contains metadata and payload reference for the step.
        /// - The caller asks for the full step.
        ///
        /// EXPECTATION:
        /// - The resolver reads the archive index.
        /// - The resolver calls IAiStepPayloadStore.LoadStepAsync.
        /// - The restored step is returned with its result data.
        ///
        /// WHY THIS MATTERS:
        /// - Full step resolution must work after retention eviction.
        /// - Payload loading should happen only when full step data is explicitly requested.
        /// </summary>
        [Fact]
        public async Task GetStepAsync_Should_Load_Payload_For_Evicted_Step()
        {
            var state = new AiExecutionState
            {
                ExecutionId = "execution-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            var archivedPayload = new AiStoredPayload
            {
                IsInline = true,
                InlineValue = new Dictionary<string, object?>
                {
                    ["value"] = "archived"
                },
                SizeBytes = 0,
                ContentType = "application/json"
            };

            var indexStore = new TestStepPayloadIndexStore(
                new Dictionary<string, AiArchivedStepPayloadIndex>
                {
                    ["step-1"] = new AiArchivedStepPayloadIndex
                    {
                        ExecutionId = state.ExecutionId,
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Completed,
                        Payload = archivedPayload,
                        ArchivedAtUtc = DateTime.UtcNow,
                        Reason = "retention"
                    }
                });

            var payloadStore = new TestStepPayloadStore();

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore
                );

            var step = await resolver.GetStepAsync(
                state.ExecutionId,
                "step-1",
                state,
                CancellationToken.None);

            Assert.NotNull(step);
            Assert.Equal("step-1", step!.StepName);
            Assert.Equal(AiStepExecutionStatus.Completed, step.Status);

            Assert.NotNull(step.Result);
            Assert.NotNull(step.Result!.Data);
            Assert.Equal("archived", step.Result.Data["value"]);

            Assert.Equal(1, indexStore.GetAsyncCallCount);
            Assert.Equal(1, payloadStore.LoadStepAsyncCallCount);
        }

        /// <summary>
        /// Verifies that WarmStepsAsync uses batch index retrieval instead of per-step calls.
        ///
        /// SCENARIO:
        /// - Multiple steps are evicted from the hot state.
        /// - The caller wants to warm them (preload metadata).
        ///
        /// EXPECTATION:
        /// - Resolver calls GetManyAsync once.
        /// - Resolver does NOT call GetAsync multiple times.
        /// - No payload is loaded.
        ///
        /// WHY THIS MATTERS:
        /// - Prevents N+1 query problem.
        /// - Critical for performance at scale.
        /// </summary>
        [Fact]
        public async Task WarmStepsAsync_Should_Use_Batch_Index_Load()
        {
            // Arrange
            var state = new AiExecutionState
            {
                ExecutionId = "execution-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            var archivedPayload = new AiStoredPayload
            {
                IsInline = true,
                InlineValue = new Dictionary<string, object?>
                {
                    ["value"] = "archived"
                },
                SizeBytes = 0,
                ContentType = "application/json"
            };

            var indexEntries = new Dictionary<string, AiArchivedStepPayloadIndex>
            {
                ["step-1"] = CreateIndex("execution-1", "step-1", archivedPayload),
                ["step-2"] = CreateIndex("execution-1", "step-2", archivedPayload),
                ["step-3"] = CreateIndex("execution-1", "step-3", archivedPayload)
            };

            var indexStore = new TestStepPayloadIndexStore(indexEntries);
            var payloadStore = new TestStepPayloadStore();

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore
                );

            var stepNames = new[] { "step-1", "step-2", "step-3" };

            // Act
            await resolver.WarmStepsAsync(
                state.ExecutionId,
                state,
                stepNames,
                CancellationToken.None);

            // Assert

            Assert.Equal(1, indexStore.GetManyAsyncCallCount);

            Assert.Equal(0, indexStore.GetAsyncCallCount);

            Assert.Equal(0, payloadStore.LoadStepAsyncCallCount);
        }

        /// <summary>
        /// Verifies that resolver returns null when step is not found
        /// in both hot state and archive index.
        ///
        /// WHY THIS MATTERS:
        /// - Prevents unexpected exceptions
        /// - Allows DAG to handle missing steps safely
        /// </summary>
        [Fact]
        public async Task GetStepAsync_Should_Return_Null_When_Step_Not_Found()
        {
            var state = new AiExecutionState
            {
                ExecutionId = "execution-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            var indexStore = new TestStepPayloadIndexStore(
                new Dictionary<string, AiArchivedStepPayloadIndex>());

            var payloadStore = new TestStepPayloadStore();

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore
                );

            var step = await resolver.GetStepAsync(
                state.ExecutionId,
                "unknown-step",
                state,
                CancellationToken.None);

            Assert.Null(step);

            Assert.Equal(1, indexStore.GetAsyncCallCount);
            Assert.Equal(0, payloadStore.LoadStepAsyncCallCount);
        }

        /// <summary>
        /// Verifies that resolver handles missing payload gracefully
        /// when archive index exists but payload load returns null.
        ///
        /// WHY THIS MATTERS:
        /// - Protects against partial corruption (index exists but payload missing)
        /// - Prevents crashes in production
        /// </summary>
        [Fact]
        public async Task GetStepAsync_Should_Handle_Missing_Payload_Gracefully()
        {
            var state = new AiExecutionState
            {
                ExecutionId = "execution-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            var indexStore = new TestStepPayloadIndexStore(
                new Dictionary<string, AiArchivedStepPayloadIndex>
                {
                    ["step-1"] = CreateIndex("execution-1", "step-1", new AiStoredPayload())
                });

            var payloadStore = new NullReturningPayloadStore();

            var resolver = new DefaultAiExecutionStepResolver(
                indexStore,
                payloadStore
                );

            var step = await resolver.GetStepAsync(
                state.ExecutionId,
                "step-1",
                state,
                CancellationToken.None);

            Assert.Null(step);

            Assert.Equal(1, indexStore.GetAsyncCallCount);
            Assert.Equal(1, payloadStore.LoadStepAsyncCallCount);
        }

        private static AiArchivedStepPayloadIndex CreateIndex(
            string executionId,
            string stepName,
            AiStoredPayload payload)
        {
            return new AiArchivedStepPayloadIndex
            {
                ExecutionId = executionId,
                StepName = stepName,
                Status = AiStepExecutionStatus.Completed,
                Payload = payload,
                ArchivedAtUtc = DateTime.UtcNow,
                Reason = "retention"
            };
        }



        /// <summary>
        /// Test payload store that records whether full archived steps are loaded.
        /// </summary>
        private sealed class TestStepPayloadStore : IAiStepPayloadStore
        {
            public int LoadStepAsyncCallCount { get; private set; }

            public Task<AiStoredPayload> SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "SaveStepAsync is not expected in resolver tests.");
            }

            public Task<AiStepState?> LoadStepAsync(
                string executionId,
                string stepName,
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                LoadStepAsyncCallCount++;

                return Task.FromResult<AiStepState?>(
                    new AiStepState
                    {
                        StepName = stepName,
                        Status = AiStepExecutionStatus.Completed,
                        Result = new AiStepResult
                        {
                            Success = true,
                            Data = payload.InlineValue as Dictionary<string, object?>
                        }
                    });
            }
        }

        private sealed class NullReturningPayloadStore : IAiStepPayloadStore
        {
            public int LoadStepAsyncCallCount { get; private set; }

            Task<AiStoredPayload> IAiStepPayloadStore.SaveStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<AiStepState?> LoadStepAsync(
                string executionId,
                string stepName,
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                LoadStepAsyncCallCount++;
                return Task.FromResult<AiStepState?>(null);
            }
        }

        /// <summary>
        /// Test archive index store used by resolver tests.
        /// </summary>
        private sealed class TestStepPayloadIndexStore : IAiStepPayloadIndexStore
        {
            private readonly IReadOnlyDictionary<string, AiArchivedStepPayloadIndex> _entries;

            public TestStepPayloadIndexStore(
                IReadOnlyDictionary<string, AiArchivedStepPayloadIndex> entries)
            {
                _entries = entries;
            }

            public int GetAsyncCallCount { get; private set; }

            public int GetManyAsyncCallCount { get; private set; }

            public Task MarkArchivedAsync(
                AiArchivedStepPayloadIndex entry,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "MarkArchivedAsync is not expected in resolver tests.");
            }

            public Task<AiArchivedStepPayloadIndex?> GetAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                GetAsyncCallCount++;

                _entries.TryGetValue(stepName, out var entry);

                return Task.FromResult(entry);
            }

            public Task<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>> GetManyAsync(
                string executionId,
                IReadOnlyCollection<string> stepNames,
                CancellationToken cancellationToken = default)
            {
                GetManyAsyncCallCount++;

                var result = _entries
                    .Where(x => stepNames.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value);

                return Task.FromResult<IReadOnlyDictionary<string, AiArchivedStepPayloadIndex>>(result);
            }

            public Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetByExecutionAsync(
                string executionId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AiArchivedStepPayloadIndex>>(
                    _entries.Values.ToList());
            }

            public Task DeleteAsync(
                string executionId,
                string stepName,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task IndexStepAsync(
                string executionId,
                string stepName,
                AiStepState step,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "IndexStepAsync is not expected in resolver tests.");
            }
        }
    }
}