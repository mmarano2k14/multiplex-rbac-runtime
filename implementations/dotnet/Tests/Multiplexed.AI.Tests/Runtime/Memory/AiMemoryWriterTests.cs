using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Memory;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Memory;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Memory
{
    /// <summary>
    /// Unit tests for <see cref="DefaultAiMemoryWriter"/>.
    ///
    /// PURPOSE:
    /// - Ensure successful step results can produce consolidated memories
    /// - Ensure failed or empty results are ignored
    /// - Ensure provenance and scoring are initialized
    ///
    /// IMPORTANT:
    /// - No DAG engine involved
    /// - No execution state mutation involved
    /// </summary>
    public sealed class AiMemoryWriterTests
    {
        [Fact]
        public async Task WriteFromStepResultAsync_Should_Create_Memory_For_Successful_Result()
        {
            var store = new InMemoryAiConsolidatedMemoryStore();
            var scoring = new DefaultAiMemoryScoringPolicy();
            var writer = new DefaultAiMemoryWriter(store, scoring);

            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1"
            };

            var result = AiStepResult.Ok(
                value: new Dictionary<string, object?>
                {
                    ["answer"] = "memory-worthy"
                },
                output: "step completed");

            var memory = await writer.WriteFromStepResultAsync(
                record,
                "compose",
                result,
                "test");

            Assert.NotNull(memory);
            Assert.Equal("test", memory!.Scope);
            Assert.Equal("execution.step.result", memory.Kind);
            Assert.Contains("exec-1", memory.ProvenanceExecutionIds);
            Assert.Contains("compose", memory.ProvenanceStepNames);
            Assert.True(memory.InitialScore > 0);
            Assert.True(memory.CurrentScore > 0);

            var stored = await store.GetAsync(memory.Id);
            Assert.NotNull(stored);
        }

        [Fact]
        public async Task WriteFromStepResultAsync_Should_Return_Null_For_Failed_Result()
        {
            var store = new InMemoryAiConsolidatedMemoryStore();
            var scoring = new DefaultAiMemoryScoringPolicy();
            var writer = new DefaultAiMemoryWriter(store, scoring);

            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1"
            };

            var result = AiStepResult.Fail("failure");

            var memory = await writer.WriteFromStepResultAsync(
                record,
                "compose",
                result,
                "test");

            Assert.Null(memory);
        }

        [Fact]
        public async Task WriteFromStepResultAsync_Should_Return_Null_For_Empty_Result()
        {
            var store = new InMemoryAiConsolidatedMemoryStore();
            var scoring = new DefaultAiMemoryScoringPolicy();
            var writer = new DefaultAiMemoryWriter(store, scoring);

            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1"
            };

            var result = AiStepResult.Ok();

            var memory = await writer.WriteFromStepResultAsync(
                record,
                "empty",
                result,
                "test");

            Assert.Null(memory);
        }

        [Fact]
        public async Task WriteFromStepResultAsync_Should_Include_Payload_Metadata_When_Result_Has_Payload()
        {
            var store = new InMemoryAiConsolidatedMemoryStore();
            var scoring = new DefaultAiMemoryScoringPolicy();
            var writer = new DefaultAiMemoryWriter(store, scoring);

            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1"
            };

            var result = AiStepResult.OkPayload(
                AiStoredPayload.Artifact("artifact-1"));

            var memory = await writer.WriteFromStepResultAsync(
                record,
                "payload-step",
                result,
                "test");

            Assert.NotNull(memory);
            Assert.True((bool)memory!.Metadata["hasPayload"]!);

            using var document = JsonDocument.Parse(memory.Content);
            var root = document.RootElement;

            Assert.Equal("payload-step", root.GetProperty("stepName").GetString());
            Assert.Equal("artifact-1", root.GetProperty("payload").GetProperty("artifactId").GetString());
        }
    }
}