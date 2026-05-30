using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.DI;
using Multiplexed.AI.DI.Cleanup;
using Multiplexed.AI.Runtime.Observability.Logging;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Fakes;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Xunit;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Validates full AI execution flow using real DI wiring and a JSON pipeline definition.
    /// </summary>
    public sealed class AiExecutionEngineJsonIntegrationTests
    {
        /// <summary>
        /// Ensures that the execution engine can create and execute a JSON-defined pipeline
        /// through the real runtime wiring.
        /// </summary>
        [Fact]
        public async Task ExecuteAllAsync_Should_Run_Full_Flow_With_Real_DI_And_Json_Definition()
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
                        "name": "hello",
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
                services.AddAiExecutionCleanup();

                services.AddMultiplexAI(configuration);

                // Test overrides only
                services.AddSingleton<Multiplexed.AI.Stores.IAiExecutionStore, FakeInMemoryExecutionStore>();
                services.AddSingleton<IContextStore, FakeInMemoryContextStore>();
                services.AddSingleton<IExecutionContextAccessor, FakeInMemoryContextAccessor>();
                services.AddSingleton<IExecutionContextFactory, FakeExecutionContextFactory>();
                services.AddSingleton<IAiRuntimeLogger, NoopLogger>();
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

                var record = await engine.CreateAsync("test-pipeline", "hello");
                var finalRecord = await engine.ExecuteAllAsync(record.ExecutionId);

                var store = provider.GetRequiredService<Multiplexed.AI.Stores.IAiExecutionStore>();
                var finalState = await store.GetStateAsync(record.ExecutionId);

                Assert.NotNull(finalState);
                Assert.True(finalRecord.IsTerminal);
                Assert.Equal(AiExecutionStatus.Completed, finalRecord.Status);
                Assert.Contains("hello", finalRecord.CompletedSteps);
                Assert.Contains("summary", finalRecord.CompletedSteps);
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }

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