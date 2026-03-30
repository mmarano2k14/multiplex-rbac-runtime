using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Integration tests for idempotent skip behavior in <see cref="AiStepExecutor"/>.
    ///
    /// This test suite validates that the executor does not re-run a step that has
    /// already been completed successfully and tracked in execution metadata.
    /// </summary>
    public sealed class AiStepExecutorIdempotencyTests
    {
        /// <summary>
        /// Validates that the step executor skips execution when the step metadata
        /// already indicates a successful completion.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Skip_When_Step_Has_Already_Completed()
        {
            // Arrange
            IAiRetryExceptionClassifier classifier = new DefaultAiRetryExceptionClassifier();
            IAiRuntimeLogger logger = new NoopLogger();

            var executor = new AiStepExecutor(classifier, logger);

            var record = new AiExecutionRecord();

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId
            };

            var stepName = "already-completed-step";

            state.Metadata[AiExecutionKeys.StepExecutionMetadata] =
                new Dictionary<string, AiStepExecutionMetadata>(StringComparer.Ordinal)
                {
                    [stepName] = new AiStepExecutionMetadata
                    {
                        StepName = stepName,
                        Status = AiStepExecutionStatus.Completed,
                        AttemptCount = 1,
                        FirstStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
                        LastStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-9),
                        CompletedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-8)
                    }
                };

            var context = new AiExecutionContext(
                record,
                state,
                new ServiceProviderStub(),
                CancellationToken.None);

            var step = new FailIfExecutedStep(stepName);
            var resolvedStep = new ResolvedAiPipelineStep
            {
                Name = stepName,
                StepKey = stepName,
                Step = step
            };

            var stepContext = new AiStepExecutionContext(
                context,
                resolvedStep);

            // Act
            var result = await executor.ExecuteAsync(resolvedStep, stepContext);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("skipped", result.Output ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            var metadataMap = Assert.IsType<Dictionary<string, AiStepExecutionMetadata>>(
                state.Metadata[AiExecutionKeys.StepExecutionMetadata]);

            var metadata = metadataMap[stepName];

            // Metadata should remain unchanged for an idempotent skip
            Assert.Equal(1, metadata.AttemptCount);
            Assert.Equal(AiStepExecutionStatus.Completed, metadata.Status);
            Assert.NotNull(metadata.CompletedAtUtc);
        }

        /// <summary>
        /// Fake step that should never be executed.
        /// If execution reaches this step, the test must fail.
        /// </summary>
        private sealed class FailIfExecutedStep : IAiStep
        {
            public FailIfExecutedStep(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("This step should have been skipped and never executed.");
            }
        }

        /// <summary>
        /// Minimal service provider stub used for execution context construction in tests.
        /// </summary>
        private sealed class ServiceProviderStub : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}