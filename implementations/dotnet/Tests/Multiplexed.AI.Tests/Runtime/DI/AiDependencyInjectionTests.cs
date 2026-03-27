using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.DI;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Models;
using Multiplexed.Rbac.Core.ExecutionContext;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.DI
{
    /// <summary>
    /// Validates runtime dependency injection wiring for the AI module.
    /// </summary>
    public sealed class AiDependencyInjectionTests
    {
        /// <summary>
        /// Ensures that the AI runtime registers the core orchestration,
        /// pipeline, and resolution services successfully.
        /// External infrastructure dependencies are replaced with in-memory
        /// test implementations so the container can be built deterministically.
        /// </summary>
        [Fact]
        public void AddMultiplexAI_Should_Register_Core_Runtime_Services()
        {
            // -----------------------------
            // Arrange
            // -----------------------------
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiEngine:DefaultPipelineDefinitionSource"] = "InMemory"
                })
                .Build();

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddOptions();

            services.AddMultiplexAI(configuration);

            // -----------------------------
            // Test overrides for external/runtime infrastructure
            // -----------------------------
            services.AddSingleton<IAiExecutionStore, FakeInMemoryExecutionStore>();
            services.AddSingleton<IContextStore, FakeInMemoryContextStore>();
            services.AddSingleton<IExecutionContextAccessor, FakeInMemoryContextAccessor>();
            services.AddSingleton<IExecutionContextFactory, FakeExecutionContextFactory>();
            services.AddSingleton<IAiRuntimeLogger, NoopLogger>();

            var provider = services.BuildServiceProvider();

            // -----------------------------
            // Act / Assert
            // -----------------------------
            Assert.NotNull(provider.GetService<IAiExecutionEngine>());
            Assert.NotNull(provider.GetService<IAiPipelineExecutor>());
            Assert.NotNull(provider.GetService<IAiPipelineDefinitionSourceSelector>());
            Assert.NotNull(provider.GetService<IAiPipelineResolver>());
            Assert.NotNull(provider.GetService<IAiStepRegistry>());
        }
    }
}