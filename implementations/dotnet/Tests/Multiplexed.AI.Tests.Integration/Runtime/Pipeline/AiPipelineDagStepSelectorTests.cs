using System.Text.Json;
using System.Text.Json.Serialization;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Pipeline;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Pipeline
{
    /// <summary>
    /// Validates DAG step selection behavior using real runtime classes.
    ///
    /// PURPOSE:
    /// - Ensures root steps are selected first.
    /// - Ensures dependency-based readiness is respected.
    /// - Ensures merge steps are blocked until all parents complete.
    /// - Ensures completed, running, and failed steps are not re-selected.
    /// - Ensures pipeline completion detection is correct.
    ///
    /// ARCHITECTURE:
    /// - <see cref="AiExecutionState"/> is treated as a persistence model.
    /// - Step-state mutation is performed through <see cref="IAiExecutionStateWriter"/>.
    /// - The selector receives the writer explicitly because readiness evaluation may
    ///   initialize missing step state or promote retry-ready steps.
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

        private static readonly IAiExecutionStateWriter StateWriter =
            new DefaultAiExecutionStateWriter();

        [Fact]
        public async Task SelectReadySteps_Should_Return_Root_Step_First()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = new AiExecutionState();

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

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
            var ready = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

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
            var ready = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

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
            var ready = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

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
            var next = AiPipelineDagStepSelector.SelectNextReadyStep(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

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
            var ready = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

            // Assert
            Assert.Empty(ready);
        }

        [Fact]
        public async Task SelectReadySteps_Should_Not_Return_Running_Steps()
        {
            // Arrange
            var pipeline = await LoadPipelineAsync("dag-parallel-basic.json");
            var state = new AiExecutionState();

            StateWriter.GetOrCreateStep(state, "start").Status = AiStepExecutionStatus.Completed;
            StateWriter.GetOrCreateStep(state, "a1").Status = AiStepExecutionStatus.Running;
            StateWriter.GetOrCreateStep(state, "a2").Status = AiStepExecutionStatus.Ready;

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

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

            StateWriter.GetOrCreateStep(state, "start").Status = AiStepExecutionStatus.Completed;
            StateWriter.GetOrCreateStep(state, "a1").Status = AiStepExecutionStatus.Failed;
            StateWriter.GetOrCreateStep(state, "a2").Status = AiStepExecutionStatus.Ready;

            // Act
            var ready = AiPipelineDagStepSelector.SelectReadySteps(
                pipeline,
                state,
                StateWriter,
                DateTime.UtcNow);

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
            var result = AiPipelineDagStepSelector.IsCompleted(
                pipeline,
                state,
                StateWriter);

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
            var result = AiPipelineDagStepSelector.IsCompleted(
                pipeline,
                state,
                StateWriter);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Loads and resolves a test pipeline definition from the test config folder.
        /// </summary>
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

        /// <summary>
        /// Creates a minimal step resolver for the pipeline definitions used in these tests.
        /// </summary>
        private static AiPipelineResolver CreateResolver()
        {
            var registry = new InMemoryAiStepRegistry();

            registry.Register(new HelloWorldTestStep());
            registry.Register(new SummaryTestStep());

            return new AiPipelineResolver(registry);
        }

        /// <summary>
        /// Creates an execution state with the supplied steps marked as completed.
        ///
        /// NOTE:
        /// - Step state is created through the writer to preserve the refactored state boundary.
        /// </summary>
        private static AiExecutionState CreateStateWithCompletedSteps(
            params string[] completedStepNames)
        {
            var state = new AiExecutionState();

            foreach (var stepName in completedStepNames)
            {
                StateWriter.GetOrCreateStep(state, stepName).Status =
                    AiStepExecutionStatus.Completed;
            }

            return state;
        }

        /// <summary>
        /// Test pipeline file root matching the JSON pipeline configuration shape.
        /// </summary>
        private sealed class TestPipelineFileRoot
        {
            public IReadOnlyCollection<AiPipelineDefinition> Pipelines { get; init; }
                = Array.Empty<AiPipelineDefinition>();
        }

        /// <summary>
        /// Minimal in-memory step registry for test pipeline resolution.
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

        /// <summary>
        /// Test step used to satisfy pipeline resolution for hello-world steps.
        /// </summary>
        private sealed class HelloWorldTestStep : IAiStep
        {
            public string Name => "hello-world";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    AiStepResult.Ok(output: "hello-world executed"));
            }
        }

        /// <summary>
        /// Test step used to satisfy pipeline resolution for summary steps.
        /// </summary>
        private sealed class SummaryTestStep : IAiStep
        {
            public string Name => "summary";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    AiStepResult.Ok(output: "summary executed"));
            }
        }
    }
}