using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Retention.Models;
using Multiplexed.AI.Runtime.Execution.Retention.Policies;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Retention
{
    /// <summary>
    /// Unit tests for the new policy-driven retention policies.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Validate retention policy decisions without involving the runtime engine,
    ///   Redis, MongoDB, payload stores, or resolvers.
    /// - Validate policy behavior through <see cref="AiRetentionContext"/>.
    /// - Validate compact, evict, and hybrid planning behavior.
    /// - Validate terminal-step filtering behavior.
    ///
    /// IMPORTANT:
    /// - These tests intentionally do not use legacy retention options.
    /// - These tests intentionally do not use the legacy retention service.
    /// - Retention configuration is supplied through <see cref="AiRetentionTriggerDefinition"/>.
    /// - These tests validate decision planning only.
    /// </remarks>
    public sealed class AiPolicyDrivenRetentionPolicyTests
    {
        /// <summary>
        /// Verifies that compaction selects only terminal steps exceeding the configured payload threshold.
        /// </summary>
        [Fact]
        public async Task CompactPolicy_Should_Select_Terminal_Steps_Exceeding_Payload_Threshold()
        {
            var policy = new CompactAiRetentionPolicy();

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["small"] = CreateCompletedStep("small", 64),
                    ["large-1"] = CreateCompletedStep("large-1", 2048),
                    ["large-2"] = CreateCompletedStep("large-2", 4096),

                    ["running"] = CreateRunningStep("running", 8192)
                }
            };

            var context = CreateContext(
                state,
                maxInlinePayloadBytes: 512);

            var result = await policy.ExecuteAsync(context);

            var decision = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result).Data;

            Assert.NotNull(decision);

            Assert.Equal(AiRetentionDecisionKind.Compact, decision.Kind);

            Assert.Contains("large-1", decision.StepsToCompact);
            Assert.Contains("large-2", decision.StepsToCompact);

            Assert.DoesNotContain("small", decision.StepsToCompact);
            Assert.DoesNotContain("running", decision.StepsToCompact);
        }

        /// <summary>
        /// Verifies that eviction selects only terminal steps.
        /// </summary>
        [Fact]
        public async Task EvictPolicy_Should_Not_Select_NonTerminal_Steps()
        {
            var policy = new EvictAiRetentionPolicy();

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["completed-1"] = CreateCompletedStep("completed-1", 100),
                    ["completed-2"] = CreateCompletedStep("completed-2", 100),

                    ["running"] = CreateRunningStep("running", 100),
                    ["ready"] = CreateReadyStep("ready"),
                    ["retry"] = CreateRetryStep("retry")
                }
            };

            var context = CreateContext(state);

            var result = await policy.ExecuteAsync(context);

            var decision = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result).Data;

            Assert.NotNull(decision);

            Assert.Contains("completed-1", decision.StepsToEvict);
            Assert.Contains("completed-2", decision.StepsToEvict);

            Assert.DoesNotContain("running", decision.StepsToEvict);
            Assert.DoesNotContain("ready", decision.StepsToEvict);
            Assert.DoesNotContain("retry", decision.StepsToEvict);
        }

        /// <summary>
        /// Verifies that Hybrid mode returns both compaction and eviction candidates.
        /// </summary>
        [Fact]
        public async Task HybridPolicy_Should_Return_Both_Compaction_And_Eviction_Candidates()
        {
            var policy = new HybridAiRetentionPolicy();

            var state = CreateCompletedState(3);

            var context = CreateContext(state);

            var result = await policy.ExecuteAsync(context);

            var decision = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result).Data;

            Assert.NotNull(decision);

            Assert.Equal(AiRetentionDecisionKind.Hybrid, decision.Kind);

            Assert.Equal(3, decision.StepsToCompact.Count);
            Assert.Equal(3, decision.StepsToEvict.Count);

            foreach (var stepName in state.Steps.Keys)
            {
                Assert.Contains(stepName, decision.StepsToCompact);
                Assert.Contains(stepName, decision.StepsToEvict);
            }
        }

        /// <summary>
        /// Verifies that Hybrid mode includes the same step in both compaction and eviction plans.
        /// </summary>
        /// <remarks>
        /// IMPORTANT:
        /// - The runtime now preserves both policy decisions.
        /// - A step selected for both actions must be compacted first and evicted afterwards.
        /// - Policies must not normalize or remove overlapping decisions.
        /// </remarks>
        [Fact]
        public async Task HybridPolicy_Should_Preserve_Both_Compaction_And_Eviction_Decisions()
        {
            var policy = new HybridAiRetentionPolicy();

            var state = CreateCompletedState(2);

            var context = CreateContext(state);

            var result = await policy.ExecuteAsync(context);

            var decision = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result).Data;

            Assert.NotNull(decision);

            Assert.Equal(2, decision.StepsToCompact.Count);
            Assert.Equal(2, decision.StepsToEvict.Count);

            foreach (var stepName in state.Steps.Keys)
            {
                Assert.Contains(stepName, decision.StepsToCompact);
                Assert.Contains(stepName, decision.StepsToEvict);
            }
        }

        /// <summary>
        /// Verifies that compaction ignores terminal steps whose payload size is below threshold.
        /// </summary>
        [Fact]
        public async Task CompactPolicy_Should_Ignore_Steps_Below_Threshold()
        {
            var policy = new CompactAiRetentionPolicy();

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = CreateCompletedStep("step-1", 64),
                    ["step-2"] = CreateCompletedStep("step-2", 128),
                    ["step-3"] = CreateCompletedStep("step-3", 256)
                }
            };

            var context = CreateContext(
                state,
                maxInlinePayloadBytes: 1024);

            var result = await policy.ExecuteAsync(context);

            var decision = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result).Data;

            Assert.Empty(decision.StepsToCompact);
        }

        /// <summary>
        /// Verifies that eviction accepts failed steps as terminal candidates.
        /// </summary>
        [Fact]
        public async Task EvictPolicy_Should_Select_Failed_Steps_As_Terminal()
        {
            var policy = new EvictAiRetentionPolicy();

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["failed"] = new AiStepState
                    {
                        StepName = "failed",
                        Status = AiStepExecutionStatus.Failed
                    },

                    ["completed"] = CreateCompletedStep("completed", 100),

                    ["running"] = CreateRunningStep("running", 100)
                }
            };

            var context = CreateContext(state);

            var result = await policy.ExecuteAsync(context);

            var decision = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result).Data;

            Assert.Contains("failed", decision.StepsToEvict);
            Assert.Contains("completed", decision.StepsToEvict);

            Assert.DoesNotContain("running", decision.StepsToEvict);
        }

        /// <summary>
        /// Verifies that disabled trigger mode compacts all terminal steps.
        /// </summary>
        [Fact]
        public async Task CompactPolicy_Should_Compact_All_Terminal_Steps_When_Trigger_Disabled()
        {
            var policy = new CompactAiRetentionPolicy();

            var state = CreateCompletedState(3);

            var context = new AiRetentionContext
            {
                ExecutionId = state.ExecutionId,
                ExecutionState = state,
                Trigger = new AiRetentionTriggerDefinition
                {
                    Enabled = false
                },
                UtcNow = DateTime.UtcNow
            };

            var result = await policy.ExecuteAsync(context);

            var decision = Assert.IsType<AiPolicyResultGeneric<AiRetentionDecision>>(result).Data;

            Assert.Equal(3, decision.StepsToCompact.Count);
        }

        /// <summary>
        /// Creates a retention context for unit-policy validation.
        /// </summary>
        private static AiRetentionContext CreateContext(
            AiExecutionState state,
            int maxInlinePayloadBytes = 512)
        {
            return new AiRetentionContext
            {
                ExecutionId = state.ExecutionId,
                ExecutionState = state,
                Trigger = new AiRetentionTriggerDefinition
                {
                    Enabled = true,
                    MaxStepsInState = 5,
                    MaxCompletedStepsInState = 5,
                    MaxInlinePayloadBytes = maxInlinePayloadBytes
                },
                UtcNow = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a deterministic completed state.
        /// </summary>
        private static AiExecutionState CreateCompletedState(
            int count)
        {
            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>()
            };

            for (var i = 0; i < count; i++)
            {
                var stepName = $"step-{i:D2}";

                state.Steps[stepName] = CreateCompletedStep(
                    stepName,
                    2048);
            }

            return state;
        }

        /// <summary>
        /// Creates a completed step with inline payload size metadata.
        /// </summary>
        private static AiStepState CreateCompletedStep(
            string name,
            long payloadSize)
        {
            return new AiStepState
            {
                StepName = name,
                Status = AiStepExecutionStatus.Completed,
                InlinePayloadSizeBytes = payloadSize,
                CompletedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            };
        }

        /// <summary>
        /// Creates a running step.
        /// </summary>
        private static AiStepState CreateRunningStep(
            string name,
            long payloadSize)
        {
            return new AiStepState
            {
                StepName = name,
                Status = AiStepExecutionStatus.Running,
                InlinePayloadSizeBytes = payloadSize
            };
        }

        /// <summary>
        /// Creates a retry step.
        /// </summary>
        private static AiStepState CreateRetryStep(
            string name)
        {
            return new AiStepState
            {
                StepName = name,
                Status = AiStepExecutionStatus.WaitingForRetry
            };
        }

        /// <summary>
        /// Creates a ready step.
        /// </summary>
        private static AiStepState CreateReadyStep(
            string name)
        {
            return new AiStepState
            {
                StepName = name,
                Status = AiStepExecutionStatus.Ready
            };
        }
    }
}
