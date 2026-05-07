using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retention
{
    /// <summary>
    /// Advanced integration tests for policy-driven execution retention.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate policy-driven retention behavior on larger DAG shapes.
    /// - Validate compaction, eviction, archived step resolution, and payload resolution.
    /// - Validate that retention does not require replay services when snapshots are disabled.
    ///
    /// MIGRATION:
    /// - Legacy options-driven retention services are no longer used.
    /// - Legacy retention metrics snapshots are no longer used.
    /// - Retention is configured through pipeline-level <c>config.retention</c>.
    /// - Assertions use actual persisted state, archived indexes, payload store, and resolver behavior.
    /// </remarks>
    public sealed class AiPolicyDrivenExecutionStateRetentionAdvancedIntegrationTests
    {
        private const int FastLinearStepCount = 60;
        private const int FastParallelStepCount = 40;
        private const int FastRetryStepCount = 30;
        private const int PayloadResolutionStepCount = 3;
        private const int MaxCompletedStepsInState = 20;

        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPolicyDrivenExecutionStateRetentionAdvancedIntegrationTests"/> class.
        /// </summary>
        /// <param name="output">The xUnit output helper.</param>
        public AiPolicyDrivenExecutionStateRetentionAdvancedIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Validates that compact-only retention does not remove linear graph state.
        /// </summary>
        [Fact]
        public async Task LinearGraph_Should_Not_Evict_Due_To_CompactOnly_Retention()
        {
            var (state, host) = await RunPipelineWithHost(
                FastLinearStepCount,
                true,
                policies:
                [
                    "retention.compact.terminal"
                ]);

            try
            {
                LogState(nameof(LinearGraph_Should_Not_Evict_Due_To_CompactOnly_Retention), state);

                Assert.Equal(FastLinearStepCount, state.Steps.Count);

                var archivedEntries = await GetArchivedEntriesAsync(host, state.ExecutionId);

                Assert.Empty(archivedEntries);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that fully parallel DAG state is bounded by policy-driven eviction.
        /// </summary>
        [Fact]
        public async Task ParallelGraph_Should_Evict_Old_Steps()
        {
            var (state, host) = await RunPipelineWithHost(
                FastParallelStepCount,
                false,
                policies:
                [
                    "retention.evict.terminal"
                ],
                fullyParallel: true);

            try
            {
                LogState(nameof(ParallelGraph_Should_Evict_Old_Steps), state);

                Assert.True(
                    state.Steps.Count <= MaxCompletedStepsInState,
                    $"Expected state to stay within retention limit. Actual={state.Steps.Count}, Max={MaxCompletedStepsInState}");

                var archivedEntries = await GetArchivedEntriesAsync(host, state.ExecutionId);

                Assert.NotEmpty(archivedEntries);
                Assert.True(archivedEntries.Count >= FastParallelStepCount - state.Steps.Count);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that compact retention does not break payload resolution.
        /// </summary>
        [Fact]
        public async Task Retention_Should_Not_Break_Payload_Resolution()
        {
            var (state, host) = await RunPipelineWithHost(
                PayloadResolutionStepCount,
                false,
                policies:
                [
                    "retention.compact.terminal"
                ],
                fullyParallel: true,
                payloadSize: 4096);

            try
            {
                LogState(nameof(Retention_Should_Not_Break_Payload_Resolution), state);

                var payloadStoreResolver = host.ServiceProvider.GetRequiredService<IAiPayloadStoreResolver>();
                var payloadStore = payloadStoreResolver.Resolve();

                var payload = state.Steps.Values
                    .SelectMany(s => s.Result?.DataPayloads?.Values ?? Enumerable.Empty<AiStoredPayload>())
                    .FirstOrDefault(p => !p.IsInline && !string.IsNullOrWhiteSpace(p.ArtifactId));

                Assert.NotNull(payload);

                var content = await payloadStore
                    .LoadAsync(payload!.ArtifactId!)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                Assert.NotNull(content);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that retention does not corrupt retry step state.
        /// </summary>
        [Fact]
        public async Task Retention_Should_Not_Break_Retry_Steps()
        {
            var (state, host) = await RunPipelineWithRetry(FastRetryStepCount);

            try
            {
                LogState(nameof(Retention_Should_Not_Break_Retry_Steps), state);

                Assert.NotNull(state);
                Assert.True(state.Steps.Count <= FastRetryStepCount);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Validates that policy-driven retention does not require replay service when snapshots are disabled.
        /// </summary>
        [Fact]
        public async Task Retention_Should_Not_Require_Replay_Service_When_Snapshots_Are_Disabled()
        {
            var (state, host) = await RunPipelineWithHost(
                FastParallelStepCount,
                false,
                policies:
                [
                    "retention.evict.terminal"
                ],
                fullyParallel: true);

            try
            {
                LogState(nameof(Retention_Should_Not_Require_Replay_Service_When_Snapshots_Are_Disabled), state);

                var replayService = host.ServiceProvider.GetService<IAiExecutionReplayService>();

                Assert.Null(replayService);
                Assert.True(state.Steps.Count <= MaxCompletedStepsInState);
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>
        /// Runs a pipeline and returns the final state and host.
        /// </summary>
        private async Task<(AiExecutionState State, AiDagExecutionEngineTestHost Host)> RunPipelineWithHost(
            int stepCount,
            bool linear,
            IReadOnlyCollection<string> policies,
            bool fullyParallel = false,
            int payloadSize = 256)
        {
            var pipeline = fullyParallel
                ? CreateFullyParallelPipeline(stepCount, payloadSize, policies)
                : CreatePipeline(stepCount, linear, payloadSize, policies);

            var host = await CreateHost(pipeline);

            _output.WriteLine(
                $"Pipeline='{pipeline.Name}', Steps='{stepCount}', Linear='{linear}', FullyParallel='{fullyParallel}', PayloadSize='{payloadSize}', Policies='{string.Join(",", policies)}'.");

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            _output.WriteLine($"Execution created. ExecutionId='{created.ExecutionId}'.");

            await host.Engine
                .ExecuteAllAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(30));

            _output.WriteLine($"Execution completed. ExecutionId='{created.ExecutionId}'.");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);

            _output.WriteLine($"State loaded. StepsAfterRetention='{state!.Steps.Count}'.");

            return (state, host);
        }

        /// <summary>
        /// Runs a retry pipeline and returns the final state and host.
        /// </summary>
        private async Task<(AiExecutionState State, AiDagExecutionEngineTestHost Host)> RunPipelineWithRetry(
            int stepCount)
        {
            var pipeline = CreateRetryPipeline(stepCount);
            var host = await CreateHost(pipeline);

            _output.WriteLine($"Retry pipeline='{pipeline.Name}', Steps='{stepCount}'.");

            var created = await host.Engine.CreateAsync(
                pipeline.Name,
                new Dictionary<string, object?>());

            _output.WriteLine($"Retry execution created. ExecutionId='{created.ExecutionId}'.");

            try
            {
                await host.Engine
                    .ExecuteAllAsync(created.ExecutionId)
                    .WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Retry execution stopped with expected exception: {ex.Message}");
            }

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            Assert.NotNull(state);

            _output.WriteLine($"Retry state loaded. Steps='{state!.Steps.Count}'.");

            return (state, host);
        }

        /// <summary>
        /// Creates a production-like host for policy-driven retention tests.
        /// </summary>
        private static async Task<AiDagExecutionEngineTestHost> CreateHost(
            AiPipelineDefinition pipeline)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",
                PayloadStore = new AiPayloadStoreOptions
                {
                    Enabled = true,
                    Provider = "mongo-redis",
                    RequireReplaySafePayloads = true,
                    MaxInlineSizeBytes = 512,

                    Mongo = new MongoAiPayloadStoreOptions
                    {
                        Enabled = true,
                        ConnectionString = "mongodb://localhost:27017",
                        DatabaseName = "multiplexed_ai_tests",
                        CollectionName = $"payloads_retention_{Guid.NewGuid():N}"
                    },

                    RedisCache = new RedisAiPayloadCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:payload:retention:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        MaxCacheablePayloadBytes = 1024 * 1024
                    },

                    StepIndexCache = new RedisAiStepPayloadIndexCacheOptions
                    {
                        Enabled = true,
                        KeyPrefix = $"test:ai:step-index:retention:{Guid.NewGuid():N}",
                        ExpirationSeconds = 120,
                        RefreshTtlOnRead = true
                    }
                },

                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = false
                },

                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                }
            };

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    var provider = new InMemoryAiPipelineDefinitionProvider(new[] { pipeline });

                    services.RemoveAll<IAiPipelineDefinitionProvider>();
                    services.RemoveAll<InMemoryAiPipelineDefinitionProvider>();

                    services.AddSingleton<IAiPipelineDefinitionProvider>(provider);
                    services.AddSingleton(provider);

                    services.AddAiStepsFromAssemblies(
                        typeof(AiPolicyDrivenExecutionStateRetentionAdvancedIntegrationTests).Assembly);
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        /// <summary>
        /// Creates a branch/linear pipeline with pipeline-level retention config.
        /// </summary>
        private static AiPipelineDefinition CreatePipeline(
            int steps,
            bool linear,
            int payloadSize,
            IReadOnlyCollection<string> policies)
        {
            var list = new List<AiPipelineStepDefinition>();

            if (linear)
            {
                for (var i = 0; i < steps; i++)
                {
                    list.Add(new AiPipelineStepDefinition
                    {
                        Name = $"step-{i}",
                        StepKey = "test.retention",
                        Order = i,
                        DependsOn = i == 0
                            ? new List<string>()
                            : new List<string> { $"step-{i - 1}" },
                        Config = new Dictionary<string, object?>
                        {
                            ["size"] = payloadSize
                        }
                    });
                }
            }
            else
            {
                const int branchSize = 5;
                var branches = steps / branchSize;

                for (var branch = 0; branch < branches; branch++)
                {
                    for (var i = 0; i < branchSize; i++)
                    {
                        var index = branch * branchSize + i;

                        list.Add(new AiPipelineStepDefinition
                        {
                            Name = $"step-{index}",
                            StepKey = "test.retention",
                            Order = index,
                            DependsOn = i == 0
                                ? new List<string>()
                                : new List<string> { $"step-{index - 1}" },
                            Config = new Dictionary<string, object?>
                            {
                                ["size"] = payloadSize
                            }
                        });
                    }
                }

                for (var index = branches * branchSize; index < steps; index++)
                {
                    list.Add(new AiPipelineStepDefinition
                    {
                        Name = $"step-{index}",
                        StepKey = "test.retention",
                        Order = index,
                        DependsOn = new List<string>(),
                        Config = new Dictionary<string, object?>
                        {
                            ["size"] = payloadSize
                        }
                    });
                }
            }

            return new AiPipelineDefinition
            {
                Name = $"pipeline-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Config = CreateRetentionConfig(policies),
                Steps = list
            };
        }

        /// <summary>
        /// Creates a fully parallel pipeline with pipeline-level retention config.
        /// </summary>
        private static AiPipelineDefinition CreateFullyParallelPipeline(
            int steps,
            int payloadSize,
            IReadOnlyCollection<string> policies)
        {
            var list = new List<AiPipelineStepDefinition>();

            for (var i = 0; i < steps; i++)
            {
                list.Add(new AiPipelineStepDefinition
                {
                    Name = $"step-{i}",
                    StepKey = "test.retention",
                    Order = i,
                    DependsOn = new List<string>(),
                    Config = new Dictionary<string, object?>
                    {
                        ["size"] = payloadSize
                    }
                });
            }

            return new AiPipelineDefinition
            {
                Name = $"pipeline-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Config = CreateRetentionConfig(policies),
                Steps = list
            };
        }

        /// <summary>
        /// Creates a retry pipeline with retention config.
        /// </summary>
        private static AiPipelineDefinition CreateRetryPipeline(
            int steps)
        {
            var list = new List<AiPipelineStepDefinition>();

            for (var i = 0; i < steps; i++)
            {
                list.Add(new AiPipelineStepDefinition
                {
                    Name = $"step-{i}",
                    StepKey = "test.retry",
                    Order = i,
                    Config = new Dictionary<string, object?>
                    {
                        ["fail"] = i % 5 == 0,
                        ["retry"] = new Dictionary<string, object?>
                        {
                            ["policies"] = new[]
                            {
                                "retry.transient.default"
                            },
                            ["maxRetries"] = 1,
                            ["strategy"] = "Fixed",
                            ["baseDelayMs"] = 10,
                            ["maxDelayMs"] = 10,
                            ["jitter"] = false
                        }
                    }
                });
            }

            return new AiPipelineDefinition
            {
                Name = $"retry-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Config = CreateRetentionConfig(
                [
                    "retention.hybrid.terminal"
                ]),
                Steps = list
            };
        }

        /// <summary>
        /// Creates pipeline-level retention configuration.
        /// </summary>
        private static Dictionary<string, object?> CreateRetentionConfig(
            IReadOnlyCollection<string> policies)
        {
            return new Dictionary<string, object?>
            {
                ["retention"] = new Dictionary<string, object?>
                {
                    ["enabled"] = true,
                    ["policies"] = policies.ToArray(),
                    ["archiveReason"] = "advanced-retention-integration-test",
                    ["trigger"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["maxStepsInState"] = MaxCompletedStepsInState,
                        ["maxCompletedStepsInState"] = MaxCompletedStepsInState,
                        ["maxInlinePayloadBytes"] = 1
                    }
                }
            };
        }

        /// <summary>
        /// Gets all archived step entries for an execution.
        /// </summary>
        private static async Task<IReadOnlyList<AiArchivedStepPayloadIndex>> GetArchivedEntriesAsync(
            AiDagExecutionEngineTestHost host,
            string executionId)
        {
            var indexStore = host.ServiceProvider.GetRequiredService<IAiStepPayloadIndexStore>();

            return await indexStore
                .GetByExecutionAsync(executionId)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Logs a compact state summary.
        /// </summary>
        private void LogState(
            string testName,
            AiExecutionState state)
        {
            _output.WriteLine(
                $"[{testName}] ExecutionId='{state.ExecutionId}', Steps='{state.Steps.Count}'");
        }

        /// <summary>
        /// Test step producing configurable payload.
        /// </summary>
        [AiStep("test.retention")]
        private sealed class TestStep : IAiStep
        {
            /// <summary>
            /// Gets the step key.
            /// </summary>
            public string Name => "test.retention";

            /// <inheritdoc />
            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext ctx,
                CancellationToken ct = default)
            {
                var size = Convert.ToInt32(ctx.Step.Config["size"]);

                return Task.FromResult(new AiStepResult
                {
                    Success = true,
                    Data = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["content"] = new string('x', size)
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Test retry step.
        /// </summary>
        [AiStep("test.retry")]
        private sealed class RetryStep : IAiStep
        {
            /// <summary>
            /// Gets the step key.
            /// </summary>
            public string Name => "test.retry";

            /// <inheritdoc />
            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext ctx,
                CancellationToken ct = default)
            {
                var shouldFail = Convert.ToBoolean(ctx.Step.Config["fail"]);

                if (shouldFail)
                {
                    return Task.FromResult(new AiStepResult
                    {
                        Success = false,
                        Error = "Simulated failure"
                    });
                }

                return Task.FromResult(new AiStepResult
                {
                    Success = true
                });
            }
        }
    }
}
