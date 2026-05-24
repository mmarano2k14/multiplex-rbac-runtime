using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Multiplexed.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Observability.Ledger
{
    /// <summary>
    /// Tests the in-memory decision ledger implementation.
    /// </summary>
    public sealed class InMemoryAiDecisionLedgerTests
    {
        /// <summary>
        /// Verifies that appended entries can be read by execution identifier.
        /// </summary>
        [Fact]
        public async Task GetByExecutionAsync_ShouldReturnEntriesForExecutionOnly()
        {
            var ledger = new InMemoryAiDecisionLedger();

            await ledger.AppendAsync(CreateEntry("execution-1", AiDecisionLedgerEvents.Execution.Started));
            await ledger.AppendAsync(CreateEntry("execution-2", AiDecisionLedgerEvents.Execution.Started));
            await ledger.AppendAsync(CreateEntry("execution-1", AiDecisionLedgerEvents.Execution.Completed));

            var entries = await ledger.GetByExecutionAsync("execution-1");

            entries.Should().HaveCount(2);
            entries.Should().OnlyContain(entry => entry.ExecutionId == "execution-1");
            entries[0].EventType.Should().Be(AiDecisionLedgerEvents.Execution.Started);
            entries[1].EventType.Should().Be(AiDecisionLedgerEvents.Execution.Completed);
        }

        /// <summary>
        /// Verifies that sequence numbers are assigned per execution stream.
        /// </summary>
        [Fact]
        public async Task AppendAsync_ShouldAssignSequencePerExecution()
        {
            var ledger = new InMemoryAiDecisionLedger();

            await ledger.AppendAsync(CreateEntry("execution-1", AiDecisionLedgerEvents.Step.Started));
            await ledger.AppendAsync(CreateEntry("execution-1", AiDecisionLedgerEvents.Step.Completed));
            await ledger.AppendAsync(CreateEntry("execution-2", AiDecisionLedgerEvents.Step.Started));

            var executionOneEntries = await ledger.GetByExecutionAsync("execution-1");
            var executionTwoEntries = await ledger.GetByExecutionAsync("execution-2");

            executionOneEntries[0].Sequence.Should().Be(1);
            executionOneEntries[1].Sequence.Should().Be(2);
            executionTwoEntries[0].Sequence.Should().Be(1);
        }

        /// <summary>
        /// Verifies that query filters return matching ledger entries.
        /// </summary>
        [Fact]
        public async Task QueryAsync_ShouldApplyFilters()
        {
            var ledger = new InMemoryAiDecisionLedger();

            await ledger.AppendAsync(CreateEntry(
                "execution-1",
                AiDecisionLedgerEvents.Concurrency.Denied,
                AiDecisionLedgerCategory.Concurrency,
                AiDecisionLedgerOutcome.Denied,
                stepId: "step-1",
                policyKey: "provider-limit"));

            await ledger.AppendAsync(CreateEntry(
                "execution-1",
                AiDecisionLedgerEvents.Step.Completed,
                AiDecisionLedgerCategory.Step,
                AiDecisionLedgerOutcome.Completed,
                stepId: "step-2"));

            var entries = await ledger.QueryAsync(new AiDecisionLedgerQuery
            {
                ExecutionId = "execution-1",
                Category = AiDecisionLedgerCategory.Concurrency,
                Outcome = AiDecisionLedgerOutcome.Denied,
                PolicyKey = "provider-limit"
            });

            entries.Should().ContainSingle();
            entries[0].EventType.Should().Be(AiDecisionLedgerEvents.Concurrency.Denied);
            entries[0].StepId.Should().Be("step-1");
        }

        /// <summary>
        /// Verifies that the default recorder builds ledger entries from the runtime correlation context.
        /// </summary>
        [Fact]
        public async Task DefaultRecorder_ShouldBuildEntryFromCorrelationContext()
        {
            var ledger = new InMemoryAiDecisionLedger();

            var recorder = new DefaultAiDecisionLedgerRecorder(
                ledger,
                Options.Create(new AiDecisionLedgerRecorderOptions
                {
                    WriteMode = AiDecisionLedgerWriteMode.Strict
                }),
                NullLogger<DefaultAiDecisionLedgerRecorder>.Instance);

            var context = new AiRuntimeCorrelationContext
            {
                ExecutionId = "execution-1",
                RunId = "run-1",
                PipelineName = "pipeline-a",
                PipelineVersion = "1.0.0",
                StepId = "step-1",
                StepKey = "rag.retrieve",
                RuntimeInstanceId = "runtime-a",
                WorkerId = "worker-1",
                ClaimToken = "claim-token-1",
                PolicyKey = "retry.default",
                Provider = "openai",
                Model = "gpt-test",
                Operation = "completion",
                TraceId = "trace-1",
                CorrelationId = "correlation-1"
            };

            await recorder.RecordAsync(
                context,
                AiDecisionLedgerCategory.Retry,
                AiDecisionLedgerEvents.Retry.Scheduled,
                AiDecisionLedgerOutcome.Applied,
                "Retry scheduled after transient failure.",
                new Dictionary<string, string>
                {
                    ["retry.count"] = "1",
                    ["retry.delay.ms"] = "500"
                });

            var entries = await ledger.GetByExecutionAsync("execution-1");

            entries.Should().ContainSingle();

            var entry = entries[0];

            entry.ExecutionId.Should().Be("execution-1");
            entry.RunId.Should().Be("run-1");
            entry.PipelineName.Should().Be("pipeline-a");
            entry.PipelineVersion.Should().Be("1.0.0");
            entry.StepId.Should().Be("step-1");
            entry.StepKey.Should().Be("rag.retrieve");
            entry.RuntimeInstanceId.Should().Be("runtime-a");
            entry.WorkerId.Should().Be("worker-1");
            entry.ClaimToken.Should().Be("claim-token-1");
            entry.PolicyKey.Should().Be("retry.default");
            entry.Provider.Should().Be("openai");
            entry.Model.Should().Be("gpt-test");
            entry.Operation.Should().Be("completion");
            entry.TraceId.Should().Be("trace-1");
            entry.CorrelationId.Should().Be("correlation-1");
            entry.Category.Should().Be(AiDecisionLedgerCategory.Retry);
            entry.EventType.Should().Be(AiDecisionLedgerEvents.Retry.Scheduled);
            entry.Outcome.Should().Be(AiDecisionLedgerOutcome.Applied);
            entry.Reason.Should().Be("Retry scheduled after transient failure.");
            entry.Metadata.Should().ContainKey("retry.count");
            entry.Metadata.Should().ContainKey("retry.delay.ms");
        }

        /// <summary>
        /// Verifies that disabled recorder mode does not append entries.
        /// </summary>
        [Fact]
        public async Task DefaultRecorder_WhenDisabled_ShouldNotAppendEntry()
        {
            var ledger = new InMemoryAiDecisionLedger();

            var recorder = new DefaultAiDecisionLedgerRecorder(
                ledger,
                Options.Create(new AiDecisionLedgerRecorderOptions
                {
                    WriteMode = AiDecisionLedgerWriteMode.Disabled
                }),
                NullLogger<DefaultAiDecisionLedgerRecorder>.Instance);

            await recorder.RecordAsync(
                new AiRuntimeCorrelationContext
                {
                    ExecutionId = "execution-1"
                },
                AiDecisionLedgerCategory.Execution,
                AiDecisionLedgerEvents.Execution.Started,
                AiDecisionLedgerOutcome.Started);

            var entries = await ledger.GetByExecutionAsync("execution-1");

            entries.Should().BeEmpty();
        }

        private static AiDecisionLedgerEntry CreateEntry(
            string executionId,
            string eventType,
            AiDecisionLedgerCategory category = AiDecisionLedgerCategory.Execution,
            AiDecisionLedgerOutcome outcome = AiDecisionLedgerOutcome.None,
            string? stepId = null,
            string? policyKey = null)
        {
            return new AiDecisionLedgerEntry
            {
                EntryId = Guid.NewGuid().ToString("N"),
                ExecutionId = executionId,
                Category = category,
                EventType = eventType,
                Outcome = outcome,
                TimestampUtc = DateTimeOffset.UtcNow,
                StepId = stepId,
                PolicyKey = policyKey
            };
        }
    }
}