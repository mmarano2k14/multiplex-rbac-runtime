using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.DI;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Fakes;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using StackExchange.Redis;
using System.Data.Common;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Validates optimistic concurrency behavior using real DI wiring
    /// and a JSON-based pipeline definition.
    /// </summary>
    public sealed class AiExecutionEngineJsonConcurrencyTests
    {
        /// <summary>
        /// Ensures that only one concurrent execution succeeds when two callers
        /// attempt to advance the same JSON-defined execution step.
        /// </summary>
        [Fact]
        public async Task ExecuteNextAsync_Should_Allow_Only_One_Concurrent_Step_Transition_With_Real_DI_And_Json_Definition()
        {
            var root = CreateTempDirectory();
            var configDir = Path.Combine(root, "Config");
            Directory.CreateDirectory(configDir);

            var jsonPath = Path.Combine(configDir, "pipelines.json");

            await File.WriteAllTextAsync(
                jsonPath,
                """
                [
                  {
                    "name": "test-pipeline",
                    "version": "1.0",
                    "steps": [
                      {
                        "name": "hello-world",
                        "stepKey": "hello-world",
                        "order": 0,
                        "input": {
                          "text": "Marco"
                        },
                        "config": {
                          "model": "gpt-4.1",
                          "delayMs": 500,
                          "maxTokens": 200,
                          "temperature": 0.7
                        }
                      }
                    ]
                  }
                ]
                """);

            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AiEngine:DefaultPipelineDefinitionSource"] = "Json",
                        ["AiEngine:JsonPipelineDefinitionFilePath"] = jsonPath
                    })
                    .Build();

                var services = new ServiceCollection();

                services.AddLogging();
                services.AddOptions();

                services.AddMultiplexAI(configuration);

                services.AddSingleton<Multiplexed.AI.Stores.IAiExecutionStore, FakeInMemoryExecutionStore>();
                services.AddSingleton<IContextStore, FakeInMemoryContextStore>();
                services.AddSingleton<IExecutionContextAccessor, FakeInMemoryContextAccessor>();
                services.AddSingleton<IExecutionContextFactory, FakeExecutionContextFactory>();
                services.AddSingleton<Multiplexed.AI.Runtime.Logging.IAiRuntimeLogger, NoopLogger>();
                services.AddSingleton<IAiDagExecutionStore, NoOpAiDagExecutionStore>();

                var provider = services.BuildServiceProvider();

                var accessor = provider.GetRequiredService<IExecutionContextAccessor>();

                accessor.Set(new ExecutionContext
                {
                    ContextKey = string.Empty,
                    Project = "Project",
                    TenantId = "tenant-id",
                    TenantGroupId = "tenant-group-id",
                    CurrentNamespace = "Namespace",
                    UserId = "user-id",
                    Namespaces = new List<NamespaceEntry>
                    {
                        new NamespaceEntry
                        {
                            Name = "Namespace",
                            Trns = new HashSet<string>
                            {
                                "trn:Project:crm:billing:invoice:read",
                                "trn:Project:crm:billing:invoice:refund"
                            }
                        }
                    },
                    TtlSeconds = 300
                });

                var engine = provider.GetRequiredService<IAiExecutionEngine>();
                var store = provider.GetRequiredService<Multiplexed.AI.Stores.IAiExecutionStore>();

                var record = await engine.CreateAsync("test-pipeline", "hello");

                using var barrier = new Barrier(2);

                var task1 = Task.Run(async () =>
                {
                    barrier.SignalAndWait();
                    return await ExecuteAndCaptureAsync(engine, record.ExecutionId);
                });

                var task2 = Task.Run(async () =>
                {
                    barrier.SignalAndWait();
                    return await ExecuteAndCaptureAsync(engine, record.ExecutionId);
                });

                var results = await Task.WhenAll(task1, task2);

                var successCount = results.Count(x => x.Exception is null);
                var failureCount = results.Count(x => x.Exception is InvalidOperationException);

                Assert.Equal(1, successCount);
                Assert.Equal(1, failureCount);

                var finalRecord = await store.GetRecordAsync(record.ExecutionId);
                var finalState = await store.GetStateAsync(record.ExecutionId);

                Assert.NotNull(finalRecord);
                Assert.NotNull(finalState);
                Assert.Single(finalRecord!.CompletedSteps);
                Assert.Contains("hello-world", finalRecord.CompletedSteps);
                Assert.True(finalRecord.IsTerminal);
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }

        /// <summary>
        /// Ensures that when ExecuteAllAsync is invoked concurrently on the same execution:
        /// - Only one caller can fully complete the pipeline
        /// - The other caller fails due to optimistic concurrency conflict
        /// - The execution state remains consistent and not duplicated
        /// </summary>
        [Fact]
        public async Task ExecuteAllAsync_Should_Allow_Only_One_Concurrent_Full_Execution()
        {
            var root = CreateTempDirectory();
            var configDir = Path.Combine(root, "Config");
            Directory.CreateDirectory(configDir);

            var jsonPath = Path.Combine(configDir, "pipelines.json");

            await File.WriteAllTextAsync(
                jsonPath,
                """
                [
                  {
                    "name": "test-pipeline",
                    "version": "1.0",
                    "steps": [
                      {
                        "name": "hello-world",
                        "stepKey": "hello-world",
                        "order": 0,
                        "input": {
                          "text": "Marco"
                        },
                        "config": {
                          "delayMs": 500
                        }
                      },
                      {
                        "name": "summary",
                        "stepKey": "summary",
                        "order": 1
                      }
                    ]
                  }
                ]
                """);

            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AiEngine:DefaultPipelineDefinitionSource"] = "Json",
                        ["AiEngine:JsonPipelineDefinitionFilePath"] = jsonPath
                    })
                    .Build();

                var services = new ServiceCollection();

                services.AddLogging();
                services.AddOptions();

                services.AddMultiplexAI(configuration);
                services.AddSingleton<IAiDagExecutionStore, NoOpAiDagExecutionStore>();

                services.AddSingleton<Multiplexed.AI.Stores.IAiExecutionStore, FakeInMemoryExecutionStore>();
                services.AddSingleton<IContextStore, FakeInMemoryContextStore>();
                services.AddSingleton<IExecutionContextAccessor, FakeInMemoryContextAccessor>();
                services.AddSingleton<IExecutionContextFactory, FakeExecutionContextFactory>();
                services.AddSingleton<Multiplexed.AI.Runtime.Logging.IAiRuntimeLogger, NoopLogger>();
                

                var provider = services.BuildServiceProvider();

                var accessor = provider.GetRequiredService<IExecutionContextAccessor>();

                accessor.Set(new ExecutionContext
                {
                    ContextKey = string.Empty,
                    Project = "Project",
                    TenantId = "tenant-id",
                    TenantGroupId = "tenant-group-id",
                    CurrentNamespace = "Namespace",
                    UserId = "user-id",
                    Namespaces = new List<NamespaceEntry>
            {
                new NamespaceEntry
                {
                    Name = "Namespace",
                    Trns = new HashSet<string>
                    {
                        "trn:Project:crm:billing:invoice:read"
                    }
                }
            },
                    TtlSeconds = 300
                });

                var engine = provider.GetRequiredService<IAiExecutionEngine>();
                var store = provider.GetRequiredService<Multiplexed.AI.Stores.IAiExecutionStore>();

                var record = await engine.CreateAsync("test-pipeline", "hello");

                using var barrier = new Barrier(2);

                var task1 = Task.Run(async () =>
                {
                    barrier.SignalAndWait();
                    return await ExecuteAllAndCaptureAsync(engine, record.ExecutionId);
                });

                var task2 = Task.Run(async () =>
                {
                    barrier.SignalAndWait();
                    return await ExecuteAllAndCaptureAsync(engine, record.ExecutionId);
                });

                var results = await Task.WhenAll(task1, task2);

                var successCount = results.Count(x => x is null);
                var failureCount = results.Count(x => x is InvalidOperationException);

                var unexpectedFailures = results
                                            .Where(x => x is not null && x is not InvalidOperationException)
                                            .ToList();

                Assert.Empty(unexpectedFailures);

                Assert.Equal(1, successCount);
                Assert.Equal(1, failureCount);

                var finalRecord = await store.GetRecordAsync(record.ExecutionId);
                var finalState = await store.GetStateAsync(record.ExecutionId);

                Assert.NotNull(finalRecord);
                Assert.NotNull(finalState);

                Assert.True(finalRecord!.IsTerminal);
                Assert.Equal(2, finalRecord.CompletedSteps.Count);

                Assert.Contains("hello-world", finalRecord.CompletedSteps);
                Assert.Contains("summary", finalRecord.CompletedSteps);
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }

        private static async Task<ExecutionAttemptOutcome> ExecuteAndCaptureAsync(
            IAiExecutionEngine engine,
            string executionId)
        {
            try
            {
                var record = await engine.ExecuteNextAsync(executionId);
                return new ExecutionAttemptOutcome(record, null);
            }
            catch (Exception ex)
            {
                return new ExecutionAttemptOutcome(null, ex);
            }
        }

        private static async Task<Exception?> ExecuteAllAndCaptureAsync(
            IAiExecutionEngine engine,
            string executionId)
        {
            try
            {
                await engine.ExecuteAllAsync(executionId);
                return (null);
            }
            catch (Exception ex)
            {
                return (ex);
            }
        }

        private sealed record ExecutionAttemptOutcome(
            AiExecutionRecord? Record,
            Exception? Exception);

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTempDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}