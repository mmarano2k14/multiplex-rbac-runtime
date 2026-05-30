using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.State;
using Multiplexed.AI.Runtime.Observability.Logging;
using Multiplexed.AI.Runtime.Pipeline.Steps.Execution;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Pipeline.Retry
{
    /// <summary>
    /// Integration tests for idempotent skip behavior in <see cref="AiStepExecutor"/>.
    ///
    /// PURPOSE:
    /// - Verifies that a completed step is not executed again.
    /// - Ensures idempotent skip behavior preserves existing metadata.
    /// - Validates compatibility with the execution state reader/writer boundary.
    /// </summary>
    public sealed class AiStepExecutorIdempotencyTests
    {
        /// <summary>
        /// Validates that the executor skips a step when metadata already marks it
        /// as completed.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Should_Skip_When_Step_Has_Already_Completed()
        {
            // Arrange
            IAiRuntimeLogger logger = new NoopLogger();

            var executor = new AiStepExecutor(logger);

            var record = new AiExecutionRecord();

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId
            };

            var stateWriter = new DefaultAiExecutionStateWriter();
            var stateReader = new DefaultAiExecutionStateReader(
                new NoopPayloadResolver());

            var stepName = "already-completed-step";

            stateWriter.SetMetadata(
                state,
                AiExecutionKeys.StepExecutionMetadata,
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
                });

            var context = new AiExecutionContext(
                record,
                state,
                new ServiceProviderStub(),
                stateReader,
                stateWriter,
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
            Assert.Contains(
                "skipped",
                result.Output ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);

            var metadataMap = Assert.IsType<Dictionary<string, AiStepExecutionMetadata>>(
                state.Metadata[AiExecutionKeys.StepExecutionMetadata]);

            var metadata = metadataMap[stepName];

            Assert.Equal(1, metadata.AttemptCount);
            Assert.Equal(AiStepExecutionStatus.Completed, metadata.Status);
            Assert.NotNull(metadata.CompletedAtUtc);
        }

        /// <summary>
        /// Fake step that must never be executed.
        /// If this step runs, idempotent skip behavior is broken.
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
                throw new InvalidOperationException(
                    "This step should have been skipped and never executed.");
            }
        }

        /// <summary>
        /// Payload resolver placeholder.
        ///
        /// This test only uses inline metadata. Payload resolution is not expected.
        /// </summary>
        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "Payload resolution is not expected in this idempotency test.");
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