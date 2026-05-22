using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Payloads
{
    /// <summary>
    /// Integration tests for payload metrics emitted by the step result payload compactor.
    ///
    /// PURPOSE:
    /// - Verifies that inline and externalized payload decisions are reflected in metrics.
    /// - Ensures metrics are emitted from the compactor, where the inline/externalized
    ///   state decision is actually applied.
    /// - Provides a reusable dynamic-step style test model for future long-run payload
    ///   and compaction scenarios.
    /// </summary>
    public sealed class AiPayloadMetricsIntegrationTests
    {
        [Fact]
        public async Task CompactAsync_Should_Record_Inline_And_Externalized_Payload_Metrics()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAiPayloadMetrics, InMemoryAiPayloadMetrics>();

            services.AddSingleton<IAiExecutionDataPolicy>(_ =>
                new TestAiExecutionDataPolicy(inlineThresholdBytes: 512));

            services.AddSingleton<IAiStepResultPayloadCompactor, DefaultAiStepResultPayloadCompactor>();

            var provider = services.BuildServiceProvider();

            var compactor = provider.GetRequiredService<IAiStepResultPayloadCompactor>();
            var metrics = (InMemoryAiPayloadMetrics)provider.GetRequiredService<IAiPayloadMetrics>();

            var result = new AiStepResult
            {
                Value = CreatePayload(size: 64),
                Data = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["small-step-output"] = CreatePayload(size: 64),
                    ["large-step-output"] = CreatePayload(size: 4096)
                }
            };

            await compactor.CompactAsync(result);

            var snapshot = metrics.Snapshot();

            Assert.Equal(2, snapshot.InlineCount);
            Assert.Equal(1, snapshot.ExternalizedCount);

            Assert.True(snapshot.InlineBytes > 0);
            Assert.True(snapshot.ExternalizedBytes > 0);

            Assert.Null(result.Payload?.ArtifactId);
            Assert.NotNull(result.DataPayloads);
            Assert.True(result.DataPayloads.ContainsKey("large-step-output"));
            Assert.False(result.DataPayloads.ContainsKey("small-step-output"));
        }

        [Fact]
        public async Task CompactAsync_Should_Record_Metrics_For_Dynamic_Number_Of_Steps()
        {
            const int stepCount = 20;
            const int inlineThresholdBytes = 512;

            var services = new ServiceCollection();

            services.AddSingleton<IAiPayloadMetrics, InMemoryAiPayloadMetrics>();

            services.AddSingleton<IAiExecutionDataPolicy>(_ =>
                new TestAiExecutionDataPolicy(inlineThresholdBytes));

            services.AddSingleton<IAiStepResultPayloadCompactor, DefaultAiStepResultPayloadCompactor>();

            var provider = services.BuildServiceProvider();

            var compactor = provider.GetRequiredService<IAiStepResultPayloadCompactor>();
            var metrics = (InMemoryAiPayloadMetrics)provider.GetRequiredService<IAiPayloadMetrics>();

            var results = Enumerable.Range(1, stepCount)
                .Select(index => new AiStepResult
                {
                    Data = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [$"step-{index}"] = CreatePayload(size: index % 2 == 0 ? 128 : 4096)
                    }
                })
                .ToList();

            foreach (var result in results)
            {
                await compactor.CompactAsync(result);
            }

            var snapshot = metrics.Snapshot();

            Assert.Equal(stepCount / 2, snapshot.InlineCount);
            Assert.Equal(stepCount / 2, snapshot.ExternalizedCount);

            Assert.True(snapshot.InlineBytes > 0);
            Assert.True(snapshot.ExternalizedBytes > snapshot.InlineBytes);
        }

        private static Dictionary<string, object?> CreatePayload(int size)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["content"] = new string('x', size)
            };
        }

        private sealed class TestAiExecutionDataPolicy : IAiExecutionDataPolicy
        {
            private readonly int _inlineThresholdBytes;

            public TestAiExecutionDataPolicy(int inlineThresholdBytes)
            {
                _inlineThresholdBytes = inlineThresholdBytes;
            }

            public Task<AiStoredPayload> StoreAsync(
                object? value,
                CancellationToken cancellationToken = default)
            {
                var sizeBytes = EstimateSizeBytes(value);
                var isInline = sizeBytes <= _inlineThresholdBytes;

                return Task.FromResult(new AiStoredPayload
                {
                    IsInline = isInline,
                    ArtifactId = isInline ? null : Guid.NewGuid().ToString("N"),
                    ContentHash = Guid.NewGuid().ToString("N"),
                    ContentType = "application/json",
                    SizeBytes = sizeBytes
                });
            }

            private static long EstimateSizeBytes(object? value)
            {
                if (value is null)
                {
                    return 0;
                }

                if (value is Dictionary<string, object?> dictionary &&
                    dictionary.TryGetValue("content", out var content) &&
                    content is string text)
                {
                    return text.Length;
                }

                return 0;
            }
        }
    }
}