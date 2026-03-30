using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Pipeline;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Pipeline
{
    /// <summary>
    /// Validates pipeline resolution using real JSON pipeline definitions
    /// stored under the test config folder.
    ///
    /// Test strategy:
    /// - use real pipeline definition models
    /// - use real resolver
    /// - use real step registry
    /// - load JSON files from config
    /// - validate execution mode, dependencies, and topology rules
    /// </summary>
    public sealed class AiPipelineResolverJsonTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [Fact]
        public async Task ResolveAsync_Should_Resolve_Basic_Dag_Pipeline_From_Config()
        {
            // Arrange
            var resolver = CreateResolver();

            var filePath = GetConfigFilePath("dag-parallel-basic.json");
            var root = await LoadRootAsync(filePath);

            var definition = Assert.Single(root.Pipelines);

            // Act
            var resolved = await resolver.ResolveAsync(definition);

            // Assert
            Assert.Equal("dag-parallel-basic", resolved.Name);
            Assert.Equal(AiExecutionMode.Dag, resolved.ExecutionMode);
            Assert.Equal(4, resolved.Steps.Count);

            Assert.Equal("start", resolved.Steps[0].Name);
            Assert.Equal("a1", resolved.Steps[1].Name);
            Assert.Equal("a2", resolved.Steps[2].Name);
            Assert.Equal("merge", resolved.Steps[3].Name);

            Assert.Empty(resolved.Steps[0].DependsOn);
            Assert.Single(resolved.Steps[1].DependsOn);
            Assert.Single(resolved.Steps[2].DependsOn);
            Assert.Equal(2, resolved.Steps[3].DependsOn.Count);
        }

        [Fact]
        public async Task ResolveAsync_Should_Resolve_Complex_Dag_Pipeline_From_Config()
        {
            // Arrange
            var resolver = CreateResolver();

            var filePath = GetConfigFilePath("dag-complex-10-steps.json");
            var root = await LoadRootAsync(filePath);

            var definition = Assert.Single(root.Pipelines);

            // Act
            var resolved = await resolver.ResolveAsync(definition);

            // Assert
            Assert.Equal("dag-complex", resolved.Name);
            Assert.Equal(AiExecutionMode.Dag, resolved.ExecutionMode);
            Assert.Equal(10, resolved.Steps.Count);

            var step1 = resolved.Steps.Single(x => x.Name == "step-1");
            var step8 = resolved.Steps.Single(x => x.Name == "step-8");
            var step10 = resolved.Steps.Single(x => x.Name == "step-10");

            Assert.Empty(step1.DependsOn);
            Assert.Equal(2, step8.DependsOn.Count);
            Assert.Equal(2, step10.DependsOn.Count);
        }

        [Fact]
        public async Task ResolveAsync_Should_Throw_When_Cycle_Is_Defined_In_Config()
        {
            // Arrange
            var resolver = CreateResolver();

            var filePath = GetConfigFilePath("dag-invalid-cycle.json");
            var root = await LoadRootAsync(filePath);

            var definition = Assert.Single(root.Pipelines);

            // Act + Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => resolver.ResolveAsync(definition));

            Assert.Contains("Circular dependency", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ResolveAsync_Should_Resolve_Wide_Parallel_Dag_From_Config()
        {
            // Arrange
            var resolver = CreateResolver();

            var filePath = GetConfigFilePath("dag-wide-parallel.json");
            var root = await LoadRootAsync(filePath);

            var definition = Assert.Single(root.Pipelines);

            // Act
            var resolved = await resolver.ResolveAsync(definition);

            // Assert
            Assert.Equal("dag-wide-parallel", resolved.Name);
            Assert.Equal(AiExecutionMode.Dag, resolved.ExecutionMode);
            Assert.Equal(7, resolved.Steps.Count);

            var rootStep = resolved.Steps.Single(x => x.Name == "root");
            var finalStep = resolved.Steps.Single(x => x.Name == "final");

            Assert.Empty(rootStep.DependsOn);
            Assert.Equal(5, finalStep.DependsOn.Count);
        }

        [Fact]
        public async Task ResolveAsync_Should_Default_To_Sequential_When_ExecutionMode_Is_Not_Specified()
        {
            // Arrange
            var resolver = CreateResolver();

            var definition = new AiPipelineDefinition
            {
                Name = "sequential-default-test",
                Steps = new[]
                {
                    new AiPipelineStepDefinition
                    {
                        Name = "hello",
                        StepKey = "hello-world",
                        Order = 1
                    },
                    new AiPipelineStepDefinition
                    {
                        Name = "summary",
                        StepKey = "summary",
                        Order = 2
                    }
                }
            };

            // Act
            var resolved = await resolver.ResolveAsync(definition);

            // Assert
            Assert.Equal(AiExecutionMode.Sequential, resolved.ExecutionMode);
            Assert.Equal(2, resolved.Steps.Count);
        }

        private static AiPipelineResolver CreateResolver()
        {
            var registry = new InMemoryAiStepRegistry();

            registry.Register(new HelloWorldTestStep());
            registry.Register(new SummaryTestStep());

            return new AiPipelineResolver(registry);
        }

        private static string GetConfigFilePath(string fileName)
        {
            return Path.Combine(AppContext.BaseDirectory, "config", fileName);
        }

        private static async Task<TestPipelineFileRoot> LoadRootAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"The test pipeline file '{filePath}' was not found.");
            }

            var json = await File.ReadAllTextAsync(filePath);

            var root = JsonSerializer.Deserialize<TestPipelineFileRoot>(json, JsonOptions);

            if (root is null)
            {
                throw new InvalidOperationException(
                    $"Unable to deserialize pipeline test file '{filePath}'.");
            }

            return root;
        }

        /// <summary>
        /// Minimal JSON root model matching:
        /// { "pipelines": [ ... ] }
        /// </summary>
        private sealed class TestPipelineFileRoot
        {
            public IReadOnlyCollection<AiPipelineDefinition> Pipelines { get; init; }
                = Array.Empty<AiPipelineDefinition>();
        }

        /// <summary>
        /// Minimal in-memory registry used by tests with real runtime resolution behavior.
        /// </summary>
        private sealed class InMemoryAiStepRegistry : IAiStepRegistry
        {
            private readonly Dictionary<string, IAiStep> _steps =
                new(StringComparer.Ordinal);

            public void Register(IAiStep step)
            {
                ArgumentNullException.ThrowIfNull(step);
                _steps[step.Name] = step;
            }

            public IAiStep Resolve(string stepKey)
            {
                if (!_steps.TryGetValue(stepKey, out var step))
                {
                    throw new InvalidOperationException(
                        $"No AI step is registered with key '{stepKey}'.");
                }

                return step;
            }
        }

        private sealed class HelloWorldTestStep : IAiStep
        {
            public string Name => "hello-world";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AiStepResult.Ok(
                    output: "hello-world executed"));
            }
        }

        private sealed class SummaryTestStep : IAiStep
        {
            public string Name => "summary";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AiStepResult.Ok(
                    output: "summary executed"));
            }
        }
    }
}