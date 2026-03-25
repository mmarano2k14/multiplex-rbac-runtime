using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Integration tests for <see cref="AiStepExecutor"/>.
    ///
    /// This test suite validates retry-oriented execution behavior, including:
    /// - attribute-driven retry policies
    /// - transient exception classification
    /// - step attempt tracking
    /// - metadata progression across retries
    /// - eventual success after transient failures
    /// </summary>
    public sealed class AiStepExecutorRetryTests
    {
        /// <summary>
        /// Validates that the step executor retries a step when a transient exception occurs
        /// and eventually succeeds once the step stops failing.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Retry_On_Transient_Exception_And_Eventually_Succeed()
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

            var context = new AiExecutionContext(
                record,
                state,
                new ServiceProviderStub(),
                CancellationToken.None);

            var step = new RetryThenSucceedStep(failuresBeforeSuccess: 2);

            // Act
            var result = await executor.ExecuteAsync(step, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("done", result.Output);
            Assert.Equal("done", result.Data["result"]);

            var metadataMap = Assert.IsType<Dictionary<string, AiStepExecutionMetadata>>(
                state.Metadata[AiExecutionKeys.StepExecutionMetadata]);

            var metadata = metadataMap[step.Name];

            Assert.Equal(3, metadata.AttemptCount);
            Assert.Equal(AiStepExecutionStatus.Succeeded, metadata.Status);
            Assert.NotNull(metadata.FirstStartedAtUtc);
            Assert.NotNull(metadata.LastStartedAtUtc);
            Assert.NotNull(metadata.CompletedAtUtc);
            Assert.Null(metadata.LastError);
            Assert.Null(metadata.LastExceptionType);
        }

        /// <summary>
        /// Fake step that throws transient timeout exceptions a fixed number of times
        /// before returning a successful result.
        /// </summary>
        [AiRetryPolicy(maxRetries: 3, delayMilliseconds: 1, BackoffMode = AiRetryBackoffMode.Fixed)]
        private sealed class RetryThenSucceedStep : IAiStep
        {
            private int _remainingFailures;

            public RetryThenSucceedStep(int failuresBeforeSuccess)
            {
                _remainingFailures = failuresBeforeSuccess;
            }

            public string Name => "retry-step";

            public Task<AiStepResult> ExecuteAsync(
                AiExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                if (_remainingFailures > 0)
                {
                    _remainingFailures--;
                    throw new TimeoutException("Transient timeout.");
                }

                return Task.FromResult(
                    AiStepResult.Ok(
                        output: "done",
                        data: new Dictionary<string, object?>
                        {
                            ["result"] = "done"
                        }));
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