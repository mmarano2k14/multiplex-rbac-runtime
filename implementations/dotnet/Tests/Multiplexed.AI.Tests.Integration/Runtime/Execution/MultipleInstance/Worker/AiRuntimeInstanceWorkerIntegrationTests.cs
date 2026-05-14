using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Stores;
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