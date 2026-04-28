using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Decisions;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Policies;
using Multiplexed.Abstractions.AI.Execution.Retention.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Retention.Services;
using Multiplexed.Abstractions.AI.Execution.Retention.Triggers;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Execution.Metrics;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Runtime.Retention;
using Multiplexed.AI.Runtime.Retention.Decisions;
using Multiplexed.AI.Runtime.Retention.Decisions.Policies;
using Multiplexed.AI.Runtime.Retention.Policies;
using Multiplexed.AI.Runtime.Retention.Triggers;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retention
{
    public sealed class AiExecutionStateRetentionAdvancedIntegrationTests
    {
        private const int FastLinearStepCount = 60;
        private const int FastParallelStepCount = 40;
        private const int FastRetryStepCount = 30;
        private const int PayloadResolutionStepCount = 3;
        private const int MaxCompletedStepsInState = 20;

        private readonly ITestOutputHelper _output;

        public AiExecutionStateRetentionAdvancedIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task LinearGraph_Should_Not_Evict_Due_To_DependencyChain()
        {
            var (_, snapshot, host) = await RunPipelineWithHost(
                FastLinearStepCount,
                true,
                AiExecutionRetentionMode.Compact);

            LogRetentionSnapshot(nameof(LinearGraph_Should_Not_Evict_Due_To_DependencyChain), snapshot);

            Assert.Equal(snapshot.TotalStepsBefore, snapshot.TotalStepsAfter);
            Assert.Equal(0, snapshot.EvictedSteps);

            await host.DisposeAsync();
        }

        [Fact]
        public async Task ParallelGraph_Should_Evict_Old_Steps()
        {
            var (_, snapshot, host) = await RunPipelineWithHost(
                FastParallelStepCount,
                false,
                AiExecutionRetentionMode.Evict,
                fullyParallel: true);

            LogRetentionSnapshot(nameof(ParallelGraph_Should_Evict_Old_Steps), snapshot);

            Assert.True(
                snapshot.EvictedSteps > 0,
                $"Expected evicted steps > 0. Before={snapshot.TotalStepsBefore}, After={snapshot.TotalStepsAfter}, Evicted={snapshot.EvictedSteps}");

            Assert.True(
                snapshot.TotalStepsAfter <= MaxCompletedStepsInState,
                $"Expected state to stay within retention limit. Before={snapshot.TotalStepsBefore}, After={snapshot.TotalStepsAfter}, Evicted={snapshot.EvictedSteps}");

            Assert.Equal(MaxCompletedStepsInState, snapshot.TotalStepsAfter);

            await host.DisposeAsync();
        }

        [Fact]
        public async Task Retention_Should_Not_Break_Payload_Resolution()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(
                PayloadResolutionStepCount,
                false,
                AiExecutionRetentionMode.Compact,
                fullyParallel: true,
                payloadSize: 4096);

            LogRetentionSnapshot(nameof(Retention_Should_Not_Break_Payload_Resolution), snapshot);

            Assert.True(
                snapshot.CompactedSteps > 0,
                $"Expected compacted steps > 0. Compacted={snapshot.CompactedSteps}");

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

            await host.DisposeAsync();
        }

        [Fact]
        public async Task Retention_Should_Not_Break_Retry_Steps()
        {
            var (state, snapshot, host) = await RunPipelineWithRetry(FastRetryStepCount);

            LogRetentionSnapshot(nameof(Retention_Should_Not_Break_Retry_Steps), snapshot);

            Assert.NotNull(state);
            Assert.True(snapshot.TotalStepsAfter <= snapshot.TotalStepsBefore);

            await host.DisposeAsync();
        }

        [Fact]
        public async Task Retention_Should_Not_Require_Replay_Service_When_Snapshots_Are_Disabled()
        {
            var (_, snapshot, host) = await RunPipelineWithHost(
                FastParallelStepCount,
                false,
                AiExecutionRetentionMode.Evict,
                fullyParallel: true);

            LogRetentionSnapshot(nameof(Retention_Should_Not_Require_Replay_Service_When_Snapshots_Are_Disabled), snapshot);

            var replayService = host.ServiceProvider.GetService<IAiExecutionReplayService>();

            Assert.Null(replayService);
            Assert.True(snapshot.TotalStepsAfter <= snapshot.TotalStepsBefore);

            await host.DisposeAsync();
        }

        private async Task<(AiExecutionState, AiExecutionRetentionServiceMetricsSnapshot, AiDagExecutionEngineTestHost)>
            RunPipelineWithHost(
                int stepCount,
                bool linear,
                AiExecutionRetentionMode mode = AiExecutionRetentionMode.Hybrid,
                bool fullyParallel = false,
                int payloadSize = 256)
        {
            var pipeline = fullyParallel
                ? CreateFullyParallelPipeline(stepCount, payloadSize)
                : CreatePipeline(stepCount, linear, payloadSize);
            var host = await CreateHost(pipeline, mode);

            _output.WriteLine($"Pipeline='{pipeline.Name}', Steps='{stepCount}', Linear='{linear}', FullyParallel='{fullyParallel}', PayloadSize='{payloadSize}'.");

            var created = await host.Engine.CreateAsync(pipeline.Name, new Dictionary<string, object?>());

            _output.WriteLine($"Execution created. ExecutionId='{created.ExecutionId}'.");

            await host.Engine
                .ExecuteAllAsync(created.ExecutionId)
                .WaitAsync(TimeSpan.FromSeconds(30));

            _output.WriteLine($"Execution completed. ExecutionId='{created.ExecutionId}'.");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            var metrics = host.ServiceProvider.GetRequiredService<IAiExecutionRetentionServiceMetrics>();
            var snapshot = ((InMemoryAiExecutionRetentionServiceMetrics)metrics).Snapshot();

            _output.WriteLine($"State loaded. StepsAfterRetention='{state?.Steps.Count ?? 0}'.");

            return (state!, snapshot, host);
        }

        private async Task<(AiExecutionState, AiExecutionRetentionServiceMetricsSnapshot, AiDagExecutionEngineTestHost)>
            RunPipelineWithRetry(int stepCount)
        {
            var pipeline = CreateRetryPipeline(stepCount);
            var host = await CreateHost(pipeline);

            _output.WriteLine($"Retry pipeline='{pipeline.Name}', Steps='{stepCount}'.");

            var created = await host.Engine.CreateAsync(pipeline.Name, new Dictionary<string, object?>());

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

            var metrics = host.ServiceProvider.GetRequiredService<IAiExecutionRetentionServiceMetrics>();
            var snapshot = ((InMemoryAiExecutionRetentionServiceMetrics)metrics).Snapshot();

            _output.WriteLine($"Retry state loaded. Steps='{state?.Steps.Count ?? 0}'.");

            return (state!, snapshot, host);
        }

        private static async Task<AiDagExecutionEngineTestHost> CreateHost(
            AiPipelineDefinition pipeline,
            AiExecutionRetentionMode mode = AiExecutionRetentionMode.Hybrid)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",

                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    MaxCompletedStepsInState = MaxCompletedStepsInState,
                    Mode = mode
                },

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
                }
            };

            options.RetentionTrigger.MaxCompletedStepsInState = options.StateRetention.MaxCompletedStepsInState;
            options.RetentionTrigger.MaxStepsInState = options.StateRetention.MaxCompletedStepsInState;
            options.RetentionTrigger.MaxInlinePayloadBytes = 1;

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
                        typeof(AiExecutionStateRetentionAdvancedIntegrationTests).Assembly);

                    services.RemoveAll<IAiExecutionRetentionPolicy>();
                    services.RemoveAll<IAiExecutionRetentionPolicyResolver>();
                    services.RemoveAll<IAiExecutionRetentionService>();
                    services.RemoveAll<IAiExecutionRetentionServiceMetrics>();

                    services.RemoveAll<IAiExecutionRetentionTrigger>();
                    services.RemoveAll<IAiExecutionRetentionDecisionEvaluator>();
                    services.RemoveAll<IAiExecutionRetentionDecisionService>();
                    services.RemoveAll<IAiExecutionRetentionDecisionPolicy>();

                    services.AddSingleton<IAiExecutionRetentionTrigger, DefaultAiExecutionRetentionTrigger>();

                    services.AddSingleton<IAiExecutionRetentionPolicy, NoopAiExecutionRetentionPolicy>();
                    services.AddSingleton<IAiExecutionRetentionPolicy, CompactAiExecutionRetentionPolicy>();
                    services.AddSingleton<IAiExecutionRetentionPolicy, EvictAiExecutionRetentionPolicy>();
                    services.AddSingleton<IAiExecutionRetentionPolicy, HybridAiExecutionRetentionPolicy>();

                    services.AddSingleton<
                        IAiExecutionRetentionPolicyResolver,
                        DefaultAiExecutionRetentionPolicyResolver>();

                    services.AddSingleton<
                        IAiExecutionRetentionDecisionEvaluator,
                        CompositeAiExecutionRetentionDecisionEvaluator>();

                    services.AddSingleton<
                        IAiExecutionRetentionDecisionService,
                        DefaultAiExecutionRetentionDecisionService>();

                    services.AddSingleton<IAiExecutionRetentionDecisionPolicy>(
                        new SizeBasedAiExecutionRetentionDecisionPolicy(1));

                    services.AddSingleton<
                        IAiExecutionRetentionServiceMetrics,
                        InMemoryAiExecutionRetentionServiceMetrics>();

                    services.AddSingleton<
                        IAiExecutionRetentionService,
                        AiExecutionRetentionService>();
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        private static AiPipelineDefinition CreatePipeline(int steps, bool linear, int payloadSize = 256)
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
                Steps = list
            };
        }

        private static AiPipelineDefinition CreateFullyParallelPipeline(int steps, int payloadSize = 256)
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
                Steps = list
            };
        }

        private static AiPipelineDefinition CreateRetryPipeline(int steps)
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
                        ["fail"] = i % 5 == 0
                    }
                });
            }

            return new AiPipelineDefinition
            {
                Name = $"retry-{Guid.NewGuid():N}",
                Version = "1",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = list
            };
        }

        private void LogRetentionSnapshot(
            string testName,
            AiExecutionRetentionServiceMetricsSnapshot snapshot)
        {
            _output.WriteLine(
                $"[{testName}] Retention service metrics: " +
                $"Mode={snapshot.LastMode}, " +
                $"Before={snapshot.TotalStepsBefore}, " +
                $"After={snapshot.TotalStepsAfter}, " +
                $"PlannedCompact={snapshot.StepsPlannedForCompaction}, " +
                $"PlannedEvict={snapshot.StepsPlannedForEviction}, " +
                $"Compacted={snapshot.CompactedSteps}, " +
                $"Evicted={snapshot.EvictedSteps}");
        }

        [AiStep("test.retention")]
        private sealed class TestStep : IAiStep
        {
            public string Name => "test.retention";

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

        [AiStep("test.retry")]
        private sealed class RetryStep : IAiStep
        {
            public string Name => "test.retry";

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

                return Task.FromResult(new AiStepResult { Success = true });
            }
        }
    }
}
