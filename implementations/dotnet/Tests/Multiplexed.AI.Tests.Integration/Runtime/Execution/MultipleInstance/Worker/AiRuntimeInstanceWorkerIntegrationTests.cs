using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Fixtures;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Infrastructure;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.MultipleInstance.Worker
{
    /// <summary>
    /// Integration tests for runtime instance worker orchestration.
    /// </summary>
    [Collection("redis")]
    public sealed class AiRuntimeInstanceWorkerIntegrationTests
    {
        /// <summary>
        /// Verifies that the runtime instance worker can run a DAG execution until terminal
        /// without callers manually invoking the execution engine loop.
        /// </summary>
        [RedisFact]
        public async Task RuntimeInstanceWorker_Should_Run_Dag_Execution_Until_Terminal()
        {
            var pipelineName = $"runtime-instance-worker-{Guid.NewGuid():N}";
            var filePath = WriteWorkerPipelineDefinitionToConfig(pipelineName);

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions($"{pipelineName}.json"));

            var worker = host.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorker>();
            var identity = host.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();
            var metrics = host.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();

            var created = await host.Engine.CreateAsync(
                pipelineName,
                "worker-test");

            try
            {
                var final = await worker.RunExecutionAsync(
                    created.ExecutionId);

                Assert.NotNull(final);
                Assert.True(final.IsTerminal);
                Assert.Equal(AiExecutionStatus.Completed, final.Status);
                Assert.Equal(4, final.CompletedSteps.Count);

                var cyclesByRuntimeInstance =
                    metrics.Worker.GetCyclesByRuntimeInstance();

                Assert.True(
                    cyclesByRuntimeInstance.TryGetValue(
                        identity.RuntimeInstanceId,
                        out var cycleCount),
                    $"No worker cycle metrics were recorded for runtime instance '{identity.RuntimeInstanceId}'.");

                Assert.True(
                    cycleCount > 0,
                    $"Expected at least one worker cycle for runtime instance '{identity.RuntimeInstanceId}'.");

                var terminalByStatus =
                    metrics.Worker.GetTerminalByStatus();

                Assert.True(
                    terminalByStatus.TryGetValue(
                        AiExecutionStatus.Completed.ToString(),
                        out var completedCount),
                    "No worker terminal metric was recorded for Completed status.");

                Assert.True(
                    completedCount > 0,
                    "Expected at least one worker terminal completion metric.");
            }
            finally
            {
                await CleanupDagExecutionAsync(
                    host.ServiceProvider,
                    created.ExecutionId);

                TryDeleteFile(filePath);
            }
        }

        /// <summary>
        /// Verifies that multiple runtime instance workers can process the same DAG execution
        /// concurrently through the worker API until the execution reaches a terminal state.
        /// </summary>
        [RedisFact]
        public async Task RuntimeInstanceWorkers_Should_Run_Same_Dag_Execution_Concurrently_Until_Terminal()
        {
            var pipelineName = $"runtime-instance-worker-group-{Guid.NewGuid():N}";
            var filePath = WriteWorkerPipelineDefinitionToConfig(pipelineName);

            await using var hostA = await CreateWorkerHostAsync(
                "runtime-worker-a",
                $"{pipelineName}.json");

            await using var hostB = await CreateWorkerHostAsync(
                "runtime-worker-b",
                $"{pipelineName}.json");

            await using var hostC = await CreateWorkerHostAsync(
                "runtime-worker-c",
                $"{pipelineName}.json");

            var workerA = hostA.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorker>();
            var workerB = hostB.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorker>();
            var workerC = hostC.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorker>();

            var identityA = hostA.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();
            var identityB = hostB.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();
            var identityC = hostC.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();

            var metricsA = hostA.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();
            var metricsB = hostB.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();
            var metricsC = hostC.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();

            var created = await hostA.Engine.CreateAsync(
                pipelineName,
                "multi-worker-api-test");

            try
            {
                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(30));

                var workerTasks = new[]
                {
            workerA.RunExecutionAsync(created.ExecutionId, timeout.Token),
            workerB.RunExecutionAsync(created.ExecutionId, timeout.Token),
            workerC.RunExecutionAsync(created.ExecutionId, timeout.Token)
        };

                var completedTask = await Task.WhenAny(workerTasks);

                var final = await completedTask;

                Assert.NotNull(final);
                Assert.True(final.IsTerminal);
                Assert.Equal(AiExecutionStatus.Completed, final.Status);
                Assert.Equal(4, final.CompletedSteps.Count);

                var dagStore = hostA.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();
                var record = await dagStore.GetRecordAsync(created.ExecutionId);

                Assert.NotNull(record);
                Assert.True(record!.IsTerminal);
                Assert.Equal(AiExecutionStatus.Completed, record.Status);

                var state = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(state);
                Assert.Equal(4, state!.Steps.Count);

                Assert.All(
                    state.Steps.Values,
                    step =>
                    {
                        Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
                        Assert.Null(step.ClaimedBy);
                        Assert.Null(step.ClaimToken);
                        Assert.Null(step.ClaimedAtUtc);
                        Assert.Null(step.LeaseExpiresAtUtc);
                    });

                AssertWorkerRecordedCycles(
                    metricsA,
                    identityA.RuntimeInstanceId);

                AssertWorkerRecordedCycles(
                    metricsB,
                    identityB.RuntimeInstanceId);

                AssertWorkerRecordedCycles(
                    metricsC,
                    identityC.RuntimeInstanceId);
            }
            finally
            {
                await CleanupDagExecutionAsync(
                    hostA.ServiceProvider,
                    created.ExecutionId);

                TryDeleteFile(filePath);
            }
        }

        /// <summary>
        /// Verifies that the runtime instance worker group can coordinate multiple runtime
        /// instance workers against the same DAG execution until a terminal state is observed.
        /// </summary>
        [RedisFact]
        public async Task RuntimeInstanceWorkerGroup_Should_Run_Same_Dag_Execution_Until_Terminal()
        {
            var pipelineName = $"runtime-instance-worker-group-api-{Guid.NewGuid():N}";
            var filePath = WriteWorkerPipelineDefinitionToConfig(pipelineName);

            await using var hostA = await CreateWorkerHostAsync(
                "runtime-worker-group-a",
                $"{pipelineName}.json");

            await using var hostB = await CreateWorkerHostAsync(
                "runtime-worker-group-b",
                $"{pipelineName}.json");

            await using var hostC = await CreateWorkerHostAsync(
                "runtime-worker-group-c",
                $"{pipelineName}.json");

            var workerA = hostA.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorker>();
            var workerB = hostB.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorker>();
            var workerC = hostC.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorker>();

            var group = hostA.ServiceProvider.GetRequiredService<IAiRuntimeInstanceWorkerGroup>();

            var identityA = hostA.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();
            var identityB = hostB.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();
            var identityC = hostC.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();

            var metricsA = hostA.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();
            var metricsB = hostB.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();
            var metricsC = hostC.ServiceProvider.GetRequiredService<IAiRuntimeMetrics>();

            var created = await hostA.Engine.CreateAsync(
                pipelineName,
                "worker-group-api-test");

            try
            {
                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(30));

                var final = await group.RunExecutionAsync(
                    created.ExecutionId,
                    new[]
                    {
                workerA,
                workerB,
                workerC
                    },
                    timeout.Token);

                Assert.NotNull(final);
                Assert.True(final.IsTerminal);
                Assert.Equal(AiExecutionStatus.Completed, final.Status);
                Assert.Equal(4, final.CompletedSteps.Count);

                var dagStore = hostA.ServiceProvider.GetRequiredService<IAiDagExecutionStore>();

                var record = await dagStore.GetRecordAsync(created.ExecutionId);

                Assert.NotNull(record);
                Assert.True(record!.IsTerminal);
                Assert.Equal(AiExecutionStatus.Completed, record.Status);

                var state = await dagStore.GetStateAsync(created.ExecutionId);

                Assert.NotNull(state);
                Assert.Equal(4, state!.Steps.Count);

                Assert.All(
                    state.Steps.Values,
                    step =>
                    {
                        Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
                        Assert.Null(step.ClaimedBy);
                        Assert.Null(step.ClaimToken);
                        Assert.Null(step.ClaimedAtUtc);
                        Assert.Null(step.LeaseExpiresAtUtc);
                    });

                AssertWorkerRecordedCycles(
                    metricsA,
                    identityA.RuntimeInstanceId);

                AssertWorkerRecordedCycles(
                    metricsB,
                    identityB.RuntimeInstanceId);

                AssertWorkerRecordedCycles(
                    metricsC,
                    identityC.RuntimeInstanceId);

                var terminalByStatus =
                    metricsA.Worker.GetTerminalByStatus();

                Assert.True(
                    terminalByStatus.TryGetValue(
                        AiExecutionStatus.Completed.ToString(),
                        out var completedCount),
                    "No worker group terminal completion was observed through worker metrics.");

                Assert.True(
                    completedCount > 0,
                    "Expected at least one Completed terminal metric from the worker group run.");
            }
            finally
            {
                await CleanupDagExecutionAsync(
                    hostA.ServiceProvider,
                    created.ExecutionId);

                TryDeleteFile(filePath);
            }
        }

        /// <summary>
        /// Creates AI engine options using a JSON pipeline definition file.
        /// </summary>
        /// <param name="jsonFileName">The JSON pipeline definition file name under the config directory.</param>
        /// <returns>The configured AI engine options.</returns>
        private static AiEngineOptions CreateOptions(
            string jsonFileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jsonFileName);

            return new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "Json",
                JsonPipelineDefinitionFilePath = "config/" + jsonFileName,
                RuntimeInstanceWorker = new AiRuntimeInstanceWorkerOptions
                {
                    MaxStepsPerCycle = 2,
                    IdleDelay = TimeSpan.FromMilliseconds(10),
                    MaxCycles = 100,
                    IgnoreConcurrencyConflicts = true
                }
            };
        }

        /// <summary>
        /// Creates a fully wired worker integration host with a deterministic runtime instance identity.
        /// </summary>
        /// <param name="runtimeInstanceId">The deterministic runtime instance identifier.</param>
        /// <param name="jsonFileName">The JSON pipeline definition file name under the config directory.</param>
        /// <returns>The created worker integration host.</returns>
        private static async Task<AiDagExecutionEngineTestHost> CreateWorkerHostAsync(
            string runtimeInstanceId,
            string jsonFileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(jsonFileName);

            return await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(jsonFileName),
                configureServices: services =>
                {
                    services.RemoveAll<IAiRuntimeInstanceIdentity>();
                    services.AddSingleton<IAiRuntimeInstanceIdentity>(
                        new TestAiRuntimeInstanceIdentity(runtimeInstanceId));
                });
        }

        /// <summary>
        /// Verifies that worker cycle metrics were recorded for the expected runtime instance.
        /// </summary>
        /// <param name="metrics">The runtime metrics facade.</param>
        /// <param name="runtimeInstanceId">The runtime instance identifier.</param>
        private static void AssertWorkerRecordedCycles(
            IAiRuntimeMetrics metrics,
            string runtimeInstanceId)
        {
            ArgumentNullException.ThrowIfNull(metrics);
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);

            var cyclesByRuntimeInstance =
                metrics.Worker.GetCyclesByRuntimeInstance();

            Assert.True(
                cyclesByRuntimeInstance.TryGetValue(
                    runtimeInstanceId,
                    out var cycleCount),
                $"No worker cycle metrics were recorded for runtime instance '{runtimeInstanceId}'.");

            Assert.True(
                cycleCount > 0,
                $"Expected at least one worker cycle for runtime instance '{runtimeInstanceId}'.");
        }

        /// <summary>
        /// Writes a deterministic DAG pipeline definition for runtime instance worker tests.
        /// </summary>
        /// <param name="pipelineName">The generated pipeline name.</param>
        /// <returns>The created file path.</returns>
        private static string WriteWorkerPipelineDefinitionToConfig(
            string pipelineName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            var definition = new AiPipelineDefinition
            {
                Name = pipelineName,
                Version = "1.0.0",
                ExecutionMode = AiExecutionMode.Dag,
                Steps = new List<AiPipelineStepDefinition>
                {
                    new()
                    {
                        Name = "step-a",
                        StepKey = "hello-world",
                        Order = 1,
                        Config = new Dictionary<string, object?>
                        {
                            ["delayMs"] = 10
                        }
                    },
                    new()
                    {
                        Name = "step-b",
                        StepKey = "hello-world",
                        Order = 2,
                        Config = new Dictionary<string, object?>
                        {
                            ["delayMs"] = 10
                        }
                    },
                    new()
                    {
                        Name = "step-c",
                        StepKey = "hello-world",
                        Order = 3,
                        DependsOn = new[] { "step-a", "step-b" },
                        Config = new Dictionary<string, object?>
                        {
                            ["delayMs"] = 10
                        }
                    },
                    new()
                    {
                        Name = "step-d",
                        StepKey = "hello-world",
                        Order = 4,
                        DependsOn = new[] { "step-c" },
                        Config = new Dictionary<string, object?>
                        {
                            ["delayMs"] = 10
                        }
                    }
                }
            };

            var root = new
            {
                pipelines = new[]
                {
                    definition
                }
            };

            var json = JsonSerializer.Serialize(
                root,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            var baseDir = AppContext.BaseDirectory;
            var configDir = Path.Combine(baseDir, "config");

            Directory.CreateDirectory(configDir);

            var filePath = Path.Combine(configDir, $"{pipelineName}.json");

            File.WriteAllText(filePath, json);

            return filePath;
        }

        /// <summary>
        /// Deletes the distributed DAG execution bundle created by the test.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve the DAG store.</param>
        /// <param name="executionId">The execution identifier.</param>
        private static async Task CleanupDagExecutionAsync(
            IServiceProvider serviceProvider,
            string executionId)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            var dagStore = serviceProvider.GetRequiredService<IAiDagExecutionStore>();

            await dagStore.DeleteExecutionBundleAsync(
                executionId);
        }

        /// <summary>
        /// Deletes a file when it exists.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        private static void TryDeleteFile(
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}