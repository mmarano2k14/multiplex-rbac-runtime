using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Pipeline.Retry;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Execution.Payloads
{
    /// <summary>
    /// Validates payload compaction behavior in <see cref="AiStepExecutor"/>.
    ///
    /// PURPOSE:
    /// - Ensure large primary values are externalized
    /// - Ensure large structured data entries are externalized
    /// - Ensure inline values are replaced with compact summaries
    /// - Ensure artifact payloads remain resolvable
    ///
    /// IMPORTANT:
    /// - This test does NOT involve DAG engine or distributed runtime
    /// - Focus is strictly on executor + payload behavior
    /// </summary>
    public sealed class AiStepExecutorPayloadCompactionTests
    {
        [Fact]
        public async Task ExecuteAsync_Should_Externalize_Large_Payload_And_Replace_Value_With_Summary()
        {
            // ---------------------------------------------------------
            // Arrange
            // ---------------------------------------------------------
            var store = new InMemoryAiPayloadStore();
            var policy = new SmartInlineAiExecutionDataPolicy(store);
            var resolver = new DefaultAiExecutionPayloadResolver(store);

            IAiRetryExceptionClassifier classifier = new DefaultAiRetryExceptionClassifier();
            var logger = new NoopLogger();

            var executor = new AiStepExecutor(classifier, logger, policy);

            var largeValue = new string('A', 5000);

            var step = new FakeLargePayloadStep(largeValue);

            var resolvedStep = new ResolvedAiPipelineStep
            {
                Name = "test-step",
                Step = step
            };

            var context = CreateStepExecutionContext(resolvedStep);

            // ---------------------------------------------------------
            // Act
            // ---------------------------------------------------------
            var result = await executor.ExecuteAsync(resolvedStep, context);

            // ---------------------------------------------------------
            // Assert - Primary payload exists and is externalized
            // ---------------------------------------------------------
            Assert.NotNull(result.Payload);
            Assert.False(result.Payload!.IsInline);

            // ---------------------------------------------------------
            // Assert - Value is replaced by compact summary
            // ---------------------------------------------------------
            var summary = Assert.IsType<Dictionary<string, object?>>(result.Value);

            Assert.True((bool)summary["payloadExternalized"]!);
            Assert.NotNull(summary["artifactId"]);

            // ---------------------------------------------------------
            // Assert - Artifact remains resolvable
            // ---------------------------------------------------------
            var resolved = await resolver.ResolveAsync(result.Payload);

            var json = Assert.IsType<JsonElement>(resolved);
            Assert.Equal(largeValue, json.GetString());
        }

        [Fact]
        public async Task ExecuteAsync_Should_Externalize_Large_Data_Entries_And_Replace_Data_With_Summary()
        {
            // ---------------------------------------------------------
            // Arrange
            // ---------------------------------------------------------
            var store = new InMemoryAiPayloadStore();
            var policy = new SmartInlineAiExecutionDataPolicy(store);
            var resolver = new DefaultAiExecutionPayloadResolver(store);

            IAiRetryExceptionClassifier classifier = new DefaultAiRetryExceptionClassifier();
            var logger = new NoopLogger();

            var executor = new AiStepExecutor(classifier, logger, policy);

            var largeDataValue = new string('B', 5000);

            var step = new FakeLargeDataStep(largeDataValue);

            var resolvedStep = new ResolvedAiPipelineStep
            {
                Name = "test-step",
                Step = step
            };

            var context = CreateStepExecutionContext(resolvedStep);

            // ---------------------------------------------------------
            // Act
            // ---------------------------------------------------------
            var result = await executor.ExecuteAsync(resolvedStep, context);

            // ---------------------------------------------------------
            // Assert - Structured data payload exists and is externalized
            // ---------------------------------------------------------
            Assert.NotNull(result.DataPayloads);
            Assert.True(result.DataPayloads!.ContainsKey("big"));

            var payload = result.DataPayloads["big"];
            Assert.False(payload.IsInline);

            // ---------------------------------------------------------
            // Assert - Inline Data entry is replaced by compact summary
            // ---------------------------------------------------------
            var summary = Assert.IsType<Dictionary<string, object?>>(result.Data["big"]);

            Assert.True((bool)summary["payloadExternalized"]!);
            Assert.NotNull(summary["artifactId"]);

            // ---------------------------------------------------------
            // Assert - Artifact remains resolvable
            // ---------------------------------------------------------
            var resolved = await resolver.ResolveAsync(payload);

            var json = Assert.IsType<JsonElement>(resolved);
            Assert.Equal(largeDataValue, json.GetString());
        }

        // ---------------------------------------------------------------------
        // TEST HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Creates a real step execution context aligned with the runtime constructor.
        /// </summary>
        private static AiStepExecutionContext CreateStepExecutionContext(
            ResolvedAiPipelineStep resolvedStep)
        {
            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1"
            };

            var state = new AiExecutionState
            {
                ExecutionId = record.ExecutionId
            };

            var services = new ServiceCollection().BuildServiceProvider();

            var executionContext = new AiExecutionContext(
                record,
                state,
                services,
                CancellationToken.None);

            return new AiStepExecutionContext(
                executionContext,
                resolvedStep);
        }

        // ---------------------------------------------------------------------
        // TEST STEPS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Fake step returning a large primary value to trigger Value externalization.
        /// </summary>
        private sealed class FakeLargePayloadStep : IAiStep
        {
            private readonly string _value;

            public FakeLargePayloadStep(string value)
            {
                _value = value;
            }

            public string Name => "fake-large-payload-step";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AiStepResult.Ok(value: _value));
            }
        }

        /// <summary>
        /// Fake step returning a large structured data value to trigger DataPayloads externalization.
        /// </summary>
        private sealed class FakeLargeDataStep : IAiStep
        {
            private readonly string _value;

            public FakeLargeDataStep(string value)
            {
                _value = value;
            }

            public string Name => "fake-large-data-step";

            public Task<AiStepResult> ExecuteAsync(
                AiStepExecutionContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AiStepResult.Ok(
                    value: null,
                    data: new Dictionary<string, object?>
                    {
                        ["big"] = _value
                    }));
            }
        }
    }
}