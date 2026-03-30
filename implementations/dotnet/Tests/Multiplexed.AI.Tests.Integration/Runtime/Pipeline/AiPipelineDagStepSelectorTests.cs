using System.Text.Json;
using System.Text.Json.Serialization;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Pipeline;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Pipeline
{
    /// <summary>
    /// Validates DAG step selection behavior using real runtime classes.
    ///
    /// This test suite ensures that:
    /// - root steps are selected first
    /// - dependency-based readiness is respected
    /// - merge steps are blocked until all parents complete
    /// - completed steps are not re-selected
    /// - pipeline completion detection is correct
    /// </summary>
    public sealed class AiPipelineDagStepSelectorTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        [Fact]
        public async Task SelectReadySteps_Should_Return_Root_Step_First()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = new AiExecutionState();

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state);

            // Assert
            var step = Assert.Single(ready);
            Assert.Equal("start", step.Name);
        }

        [Fact]
        public async Task SelectReadySteps_Should_Return_Parallel_Steps_After_Root_Completes()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = CreateStateWithCompletedSteps("start");

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state);

            // Assert
            Assert.Equal(2, ready.Count);
            Assert.Contains(ready, x => x.Name == "a1");
            Assert.Contains(ready, x => x.Name == "a2");
        }

        [Fact]
        public async Task SelectReadySteps_Should_Not_Return_Merge_If_Only_One_Parent_Is_Completed()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = CreateStateWithCompletedSteps("start", "a1");

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state);

            // Assert
            var step = Assert.Single(ready);
            Assert.Equal("a2", step.Name);
        }

        [Fact]
        public async Task SelectReadySteps_Should_Return_Merge_When_All_Parents_Are_Completed()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = CreateStateWithCompletedSteps("start", "a1", "a2");

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state);

            // Assert
            var step = Assert.Single(ready);
            Assert.Equal("merge", step.Name);
        }

        [Fact]
        public async Task SelectNextReadyStep_Should_Return_First_Ready_Step_By_Order()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = CreateStateWithCompletedSteps("start");

            // Act
            var next = AiPipelineDagStepSelector.SelectNextReadyStep(pipeline, state);

            // Assert
            Assert.NotNull(next);
            Assert.Equal("a1", next!.Name);
        }

        [Fact]
        public async Task SelectReadySteps_Should_Not_Return_Completed_Steps()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = CreateStateWithCompletedSteps("start", "a1", "a2", "merge");

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state);

            // Assert
            Assert.Empty(ready);
        }

        [Fact]
        public async Task SelectReadySteps_Should_Not_Return_Running_Steps()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = new AiExecutionState();

            var start = state.GetOrCreateStep("start");
            start.Status = AiStepExecutionStatus.Completed;

            var a1 = state.GetOrCreateStep("a1");
            a1.Status = AiStepExecutionStatus.Running;

            var a2 = state.GetOrCreateStep("a2");
            a2.Status = AiStepExecutionStatus.Pending;

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state);

            // Assert
            var step = Assert.Single(ready);
            Assert.Equal("a2", step.Name);
        }

        [Fact]
        public async Task SelectReadySteps_Should_Not_Return_Failed_Steps()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = new AiExecutionState();

            var start = state.GetOrCreateStep("start");
            start.Status = AiStepExecutionStatus.Completed;

            var a1 = state.GetOrCreateStep("a1");
            a1.Status = AiStepExecutionStatus.Failed;

            var a2 = state.GetOrCreateStep("a2");
            a2.Status = AiStepExecutionStatus.Pending;

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(pipeline, state);

            // Assert
            var step = Assert.Single(ready);
            Assert.Equal("a2", step.Name);
        }

        [Fact]
        public async Task IsCompleted_Should_Return_False_When_Not_All_Steps_Are_Completed()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = CreateStateWithCompletedSteps("start", "a1");

            // Act
            var result = AiPipelineDagStepSelector.IsCompleted(pipeline, state);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsCompleted_Should_Return_True_When_All_Steps_Are_Completed()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = CreateStateWithCompletedSteps("start", "a1", "a2", "merge");

            // Act
            var result = AiPipelineDagStepSelector.IsCompleted(pipeline, state);

            // Assert
            Assert.True(result);
        }

        private static async Task<ResolvedAiPipeline> LoadPipelineAsync(string fileName)
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "config", fileName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"The test pipeline file '{filePath}' was not found.");
            }

            var json = await File.ReadAllTextAsync(filePath);

            var root = JsonSerializer.Deserialize<TestPipelineFileRoot>(json, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Unable to deserialize pipeline file '{filePath}'.");

            var definition = Assert.Single(root.Pipelines);

            var resolver = CreateResolver();

            return await resolver.ResolveAsync(definition);
        }

        private static AiPipelineResolver CreateResolver()
        {
            var registry = new InMemoryAiStepRegistry();

            registry.Register(new HelloWorldTestStep());
            registry.Register(new SummaryTestStep());

            return new AiPipelineResolver(registry);
        }

        private static AiExecutionState CreateStateWithCompletedSteps(params string[] completedStepNames)
        {
            var state = new AiExecutionState();

            foreach (var stepName in completedStepNames)
            {
                var step = state.GetOrCreateStep(stepName);
                step.Status = AiStepExecutionStatus.Completed;
            }

            return state;
        }

        private sealed class TestPipelineFileRoot
        {
            public IReadOnlyCollection<AiPipelineDefinition> Pipelines { get; init; }
                = Array.Empty<AiPipelineDefinition>();
        }

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