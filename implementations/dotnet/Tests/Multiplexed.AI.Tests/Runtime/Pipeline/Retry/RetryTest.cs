using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry.old;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Integration tests for <see cref="AiStepExecutor"/>.
    ///
    /// PURPOSE:
    /// - Validate retry-oriented execution behavior.
    /// - Verify attribute-driven retry policies.
    /// - Verify transient exception classification.
    /// - Verify attempt tracking and retry metadata.
    /// - Verify eventual success after transient failures.
    ///
    /// ARCHITECTURE:
    /// - Retry metadata is stored in execution metadata.
    /// - State mutation goes through <see cref="IAiExecutionStateWriter"/>.
    /// - State reading goes through <see cref="IAiExecutionStateReader"/>.
    /// - <see cref="AiExecutionState"/> remains a persistence model only.
    /// </summary>
    public sealed class AiStepExecutorRetryTests
    {
        /// <summary>
        /// Validates that the executor retries a step after transient exceptions
        /// and eventually returns the successful result.
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

            IAiExecutionStateWriter stateWriter = new DefaultAiExecutionStateWriter();
            IAiExecutionStateReader stateReader = new DefaultAiExecutionStateReader(
                new NoopPayloadResolver());

            var context = new AiExecutionContext(
                record,
                state,
                new ServiceProviderStub(),
                stateReader,
                stateWriter,
                CancellationToken.None);

            var step = new RetryThenSucceedStep(failuresBeforeSuccess: 2);

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
            var result = await executor.ExecuteAsync(resolvedStep, stepContext);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("done", result.Output);
            Assert.Equal("done", result.Data["result"]);

            var metadataMap = Assert.IsType<Dictionary<string, AiStepExecutionMetadata>>(
                state.Metadata[AiExecutionKeys.StepExecutionMetadata]);

            var metadata = metadataMap[step.Name];

            Assert.Equal(3, metadata.AttemptCount);
            Assert.Equal(AiStepExecutionStatus.Completed, metadata.Status);
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
                AiStepExecutionContext context,
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
        /// Payload resolver placeholder.
        ///
        /// This test only uses inline retry metadata. Payload resolution is not expected.
        /// </summary>
        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this retry success test.");
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