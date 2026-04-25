using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.Runtime.Execution.Metrics;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Multiplexed.AI.Runtime.Execution.Retention;
using Multiplexed.AI.Runtime.Pipeline.Definition;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Retention
{
    public sealed class AiExecutionStateRetentionAdvancedIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public AiExecutionStateRetentionAdvancedIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task LinearGraph_Should_Not_Evict_Due_To_DependencyChain()
        {
            var (_, snapshot, host) = await RunPipelineWithHost(200, true);

            LogRetentionSnapshot(nameof(LinearGraph_Should_Not_Evict_Due_To_DependencyChain), snapshot);

            Assert.Equal(snapshot.TotalStepsBefore, snapshot.TotalStepsAfter);
            Assert.Equal(0, snapshot.EvictedSteps);

            await host.DisposeAsync();
        }

        [Fact]
        public async Task ParallelGraph_Should_Evict_Old_Steps()
        {
            var (_, snapshot, host) = await RunPipelineWithHost(200, false);

            LogRetentionSnapshot(nameof(ParallelGraph_Should_Evict_Old_Steps), snapshot);

            Assert.True(
                snapshot.EvictedSteps > 0,
                $"Expected evicted steps > 0. Before={snapshot.TotalStepsBefore}, After={snapshot.TotalStepsAfter}, Evicted={snapshot.EvictedSteps}");

            Assert.True(
                snapshot.TotalStepsAfter < snapshot.TotalStepsBefore,
                $"Expected state reduction. Before={snapshot.TotalStepsBefore}, After={snapshot.TotalStepsAfter}, Evicted={snapshot.EvictedSteps}");

            await host.DisposeAsync();
        }

        [Fact]
        public async Task Retention_Should_Not_Break_Payload_Resolution()
        {
            var (state, snapshot, host) = await RunPipelineWithHost(200, false);

            LogRetentionSnapshot(nameof(Retention_Should_Not_Break_Payload_Resolution), snapshot);

            var payloadStoreResolver = host.ServiceProvider.GetRequiredService<IAiPayloadStoreResolver>();
            var payloadStore = payloadStoreResolver.Resolve();

            var payload = state.Steps.Values
                .SelectMany(s => s.Result?.DataPayloads?.Values ?? Enumerable.Empty<AiStoredPayload>())
                .FirstOrDefault(p => !p.IsInline && !string.IsNullOrWhiteSpace(p.ArtifactId));

            Assert.NotNull(payload);

            var content = await payloadStore.LoadAsync(payload!.ArtifactId!);

            Assert.NotNull(content);

            await host.DisposeAsync();
        }

        [Fact]
        public async Task Retention_Should_Not_Break_Retry_Steps()
        {
            var (state, snapshot, host) = await RunPipelineWithRetry(50);

            LogRetentionSnapshot(nameof(Retention_Should_Not_Break_Retry_Steps), snapshot);

            Assert.NotNull(state);
            Assert.True(snapshot.TotalStepsAfter <= snapshot.TotalStepsBefore);

            await host.DisposeAsync();
        }

        [Fact]
        public async Task Retention_Should_Not_Require_Replay_Service_When_Snapshots_Are_Disabled()
        {
            var (_, snapshot, host) = await RunPipelineWithHost(200, false);

            LogRetentionSnapshot(nameof(Retention_Should_Not_Require_Replay_Service_When_Snapshots_Are_Disabled), snapshot);

            var replayService = host.ServiceProvider.GetService<IAiExecutionReplayService>();

            Assert.Null(replayService);
            Assert.True(snapshot.TotalStepsAfter <= snapshot.TotalStepsBefore);

            await host.DisposeAsync();
        }

        private async Task<(AiExecutionState, AiExecutionRetentionMetricsSnapshot, AiDagExecutionEngineTestHost)>
            RunPipelineWithHost(int stepCount, bool linear)
        {
            var pipeline = CreatePipeline(stepCount, linear);
            var host = await CreateHost(pipeline);

            _output.WriteLine($"Pipeline='{pipeline.Name}', Steps='{stepCount}', Linear='{linear}'.");

            var created = await host.Engine.CreateAsync(pipeline.Name, new Dictionary<string, object?>());

            _output.WriteLine($"Execution created. ExecutionId='{created.ExecutionId}'.");

            await host.Engine.ExecuteAllAsync(created.ExecutionId);

            _output.WriteLine($"Execution completed. ExecutionId='{created.ExecutionId}'.");

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            var metrics = host.ServiceProvider.GetRequiredService<IAiExecutionRetentionMetrics>();
            var snapshot = ((InMemoryAiExecutionRetentionMetrics)metrics).Snapshot();

            _output.WriteLine($"State loaded. StepsAfterRetention='{state?.Steps.Count ?? 0}'.");

            return (state!, snapshot, host);
        }

        private async Task<(AiExecutionState, AiExecutionRetentionMetricsSnapshot, AiDagExecutionEngineTestHost)>
            RunPipelineWithRetry(int stepCount)
        {
            var pipeline = CreateRetryPipeline(stepCount);
            var host = await CreateHost(pipeline);

            _output.WriteLine($"Retry pipeline='{pipeline.Name}', Steps='{stepCount}'.");

            var created = await host.Engine.CreateAsync(pipeline.Name, new Dictionary<string, object?>());

            _output.WriteLine($"Retry execution created. ExecutionId='{created.ExecutionId}'.");

            try
            {
                await host.Engine.ExecuteAllAsync(created.ExecutionId);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Retry execution stopped with expected exception: {ex.Message}");
            }

            var dagStore = host.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
            var state = await dagStore.GetStateAsync(created.ExecutionId);

            var metrics = host.ServiceProvider.GetRequiredService<IAiExecutionRetentionMetrics>();
            var snapshot = ((InMemoryAiExecutionRetentionMetrics)metrics).Snapshot();

            _output.WriteLine($"Retry state loaded. Steps='{state?.Steps.Count ?? 0}'.");

            return (state!, snapshot, host);
        }

        private static async Task<AiDagExecutionEngineTestHost> CreateHost(AiPipelineDefinition pipeline)
        {
            var options = new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory",

                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    MaxCompletedStepsInState = 50
                },

                PayloadStore = new AiPayloadStoreOptions
                {
                    Enabled = true,
                    Provider = "mongo-redis",
                    RequireReplaySafePayloads = true,
                    MaxInlineSizeBytes = 1024,
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
                    }
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
                        typeof(AiExecutionStateRetentionAdvancedIntegrationTests).Assembly);

                    services.RemoveAll<IAiExecutionRetentionMetrics>();
                    services.RemoveAll<IAiExecutionStateRetentionPolicy>();

                    services.AddSingleton<IAiExecutionRetentionMetrics, InMemoryAiExecutionRetentionMetrics>();

                    services.AddSingleton<IAiExecutionStateRetentionPolicy>(sp =>
                    {
                        var metrics = sp.GetRequiredService<IAiExecutionRetentionMetrics>();

                        return new DefaultAiExecutionStateRetentionPolicy(
                            options.StateRetention,
                            metrics);
                    });
                },
                "mongodb://localhost:27017",
                "multiplexed_ai_tests",
                "localhost:6379");
        }

        private static AiPipelineDefinition CreatePipeline(int steps, bool linear)
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
                            ["size"] = i % 2 == 0 ? 128 : 4096
                        }
                    });
                }
            }
            else
            {
                const int branchSize = 10;
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
                                ["size"] = index % 2 == 0 ? 128 : 4096
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
                            ["size"] = index % 2 == 0 ? 128 : 4096
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
            AiExecutionRetentionMetricsSnapshot snapshot)
        {
            _output.WriteLine(
                $"[{testName}] Retention metrics: " +
                $"Before={snapshot.TotalStepsBefore}, " +
                $"After={snapshot.TotalStepsAfter}, " +
                $"Evicted={snapshot.EvictedSteps}, " +
                $"Active={snapshot.ActiveSteps}, " +
                $"Pending={snapshot.PendingSteps}");
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