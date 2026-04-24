using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Integration tests for terminal retry failure behavior in <see cref="AiStepExecutor"/>.
    /// </summary>
    public sealed class AiStepExecutorFailureTests
    {
        /// <summary>
        /// Validates that the step executor stops retrying when the configured retry limit is exceeded
        /// and rethrows the final exception.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Fail_When_MaxRetries_Are_Exceeded()
        {
            // Arrange
            IAiRetryExceptionClassifier classifier = new DefaultAiRetryExceptionClassifier();
            IAiRuntimeLogger logger = new NoopLogger();

            var dataPolicy = new InlineAiExecutionDataPolicy();

            var executor = new AiStepExecutor(classifier, logger);

            var record = new AiExecutionRecord();

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId
            };

            var context = new AiExecutionContext(
                record,
                state,
                new ServiceProviderStub(),
                CancellationToken.None);

            var step = new AlwaysTimeoutStep();

            var resolvedStep = new ResolvedAiPipelineStep
            {
                Name = step.Name,
                StepKey = step.Name,
                Step = step
            };

            var stepContext = new AiStepExecutionContext(
                context,
                resolvedStep);

            // Act
            var exception = await Assert.ThrowsAsync<TimeoutException>(
                () => executor.ExecuteAsync(resolvedStep, stepContext));

            // Assert
            Assert.Equal("Always failing timeout.", exception.Message);

            var metadataMap = Assert.IsType<Dictionary<string, AiStepExecutionMetadata>>(
                state.Metadata[AiExecutionKeys.StepExecutionMetadata]);

            var metadata = metadataMap[step.Name];

            // Initial attempt + configured retries
            Assert.Equal(3, metadata.AttemptCount);
            Assert.Equal(AiStepExecutionStatus.Failed, metadata.Status);
            Assert.NotNull(metadata.FirstStartedAtUtc);
            Assert.NotNull(metadata.LastStartedAtUtc);
            Assert.Null(metadata.CompletedAtUtc);
            Assert.Equal("Always failing timeout.", metadata.LastError);
            Assert.Equal(typeof(TimeoutException).FullName, metadata.LastExceptionType);
        }

        /// <summary>
        /// Fake step that always throws a transient timeout exception.
        /// </summary>
        [AiRetryPolicy(maxRetries: 2, delayMilliseconds: 1, BackoffMode = AiRetryBackoffMode.Fixed)]
        private sealed class AlwaysTimeoutStep : IAiStep
        {
            public string Name => "always-timeout-step";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                throw new TimeoutException("Always failing timeout.");
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