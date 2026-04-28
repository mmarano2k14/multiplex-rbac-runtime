using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Retention;
using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Retention.Policies;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Retention
{
    /// <summary>
    /// Unit tests for the AI execution retention policies.
    ///
    /// PURPOSE:
    /// - Validate retention policy decisions without involving the engine, Redis, MongoDB, payload store, or resolver.
    /// - Ensure eviction respects the configured hot-state limit.
    /// - Ensure non-terminal steps are never evicted.
    /// - Ensure dependency-safe eviction so completed parents required by active children remain in the hot state.
    /// - Ensure Hybrid mode separates compaction candidates from eviction candidates.
    ///
    /// IMPORTANT:
    /// - These tests validate policy planning only.
    /// - They do not validate RetentionService persistence/index/remove ordering.
    /// - They intentionally protect against retention loops caused by removing DAG dependencies too early.
    /// </summary>
    public sealed class AiExecutionRetentionPolicyTests
    {
        /// <summary>
        /// Verifies that Evict mode selects the oldest completed steps when the state
        /// contains more steps than MaxCompletedStepsInState.
        ///
        /// EXPECTATION:
        /// - Only completed steps are selected for eviction.
        /// - No compaction is requested in Evict mode.
        /// - The number of evicted steps equals the overflow above the configured limit.
        /// </summary>
        [Fact]
        public async Task EvictPolicy_Should_Evict_Oldest_Completed_Steps_When_Above_MaxCompletedStepsInState()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Evict,
                    MaxCompletedStepsInState = 5
                }
            });

            var policy = new EvictAiExecutionRetentionPolicy(options);
            var state = CreateStateWithCompletedSteps(10);

            var decision = await policy.EvaluateAsync(state, CancellationToken.None);

            Assert.NotNull(decision);
            Assert.Equal(5, decision.StepsToEvict.Count);
            Assert.Empty(decision.StepsToCompact);

            Assert.All(
                decision.StepsToEvict,
                stepName =>
                {
                    Assert.True(state.Steps.ContainsKey(stepName));
                    Assert.Equal(AiStepExecutionStatus.Completed, state.Steps[stepName].Status);
                });
        }

        /// <summary>
        /// Verifies that Evict mode never selects non-terminal steps for removal.
        ///
        /// EXPECTATION:
        /// - Running, WaitingForRetry, and Ready steps are never evicted.
        /// - Only completed steps can appear in StepsToEvict.
        ///
        /// WHY THIS MATTERS:
        /// - Removing non-terminal steps can corrupt execution progress and break convergence.
        /// </summary>
        [Fact]
        public async Task EvictPolicy_Should_Not_Evict_NonTerminal_Steps()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Evict,
                    MaxCompletedStepsInState = 2
                }
            });

            var policy = new EvictAiExecutionRetentionPolicy(options);

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = CreateStep("step-1", AiStepExecutionStatus.Completed, -3),
                    ["step-2"] = CreateStep("step-2", AiStepExecutionStatus.Completed, -2),
                    ["step-3"] = CreateStep("step-3", AiStepExecutionStatus.Completed, -1),

                    ["step-4"] = CreateStep("step-4", AiStepExecutionStatus.Running, 0),
                    ["step-5"] = CreateStep("step-5", AiStepExecutionStatus.WaitingForRetry, 0),
                    ["step-6"] = CreateStep("step-6", AiStepExecutionStatus.Ready, 0),
                }
            };

            var decision = await policy.EvaluateAsync(state, CancellationToken.None);

            Assert.NotNull(decision);
            Assert.True(decision.StepsToEvict.Count > 0);

            Assert.DoesNotContain("step-4", decision.StepsToEvict);
            Assert.DoesNotContain("step-5", decision.StepsToEvict);
            Assert.DoesNotContain("step-6", decision.StepsToEvict);

            Assert.All(
                decision.StepsToEvict,
                stepName =>
                {
                    Assert.True(state.Steps.ContainsKey(stepName));
                    Assert.Equal(AiStepExecutionStatus.Completed, state.Steps[stepName].Status);
                });
        }

        /// <summary>
        /// Verifies that Hybrid mode behaves as compaction-only when the hot state
        /// is still under the configured retention threshold.
        ///
        /// EXPECTATION:
        /// - No steps are evicted.
        /// - All terminal steps are selected for compaction.
        ///
        /// WHY THIS MATTERS:
        /// - Hybrid should reduce payload size before it starts removing step entries.
        /// </summary>
        [Fact]
        public async Task HybridPolicy_Should_Only_Compact_When_Under_Threshold()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Hybrid,
                    MaxCompletedStepsInState = 5
                }
            });

            var policy = new HybridAiExecutionRetentionPolicy(options);

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = CreateStep("step-1", AiStepExecutionStatus.Completed, -3),
                    ["step-2"] = CreateStep("step-2", AiStepExecutionStatus.Completed, -2),
                    ["step-3"] = CreateStep("step-3", AiStepExecutionStatus.Completed, -1),
                }
            };

            var decision = await policy.EvaluateAsync(state, CancellationToken.None);

            Assert.Empty(decision.StepsToEvict);
            Assert.Equal(3, decision.StepsToCompact.Count);

            Assert.All(
                decision.StepsToCompact,
                stepName =>
                {
                    var step = state.Steps[stepName];
                    Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
                });
        }

        /// <summary>
        /// Verifies that Hybrid mode evicts only the overflow and compacts the remaining
        /// terminal steps when the hot state is above the configured threshold.
        ///
        /// EXPECTATION:
        /// - The oldest completed step is evicted.
        /// - The remaining completed steps are compacted.
        /// - A step is never both compacted and evicted in the same plan.
        ///
        /// WHY THIS MATTERS:
        /// - The retention plan must be unambiguous.
        /// - Eviction and compaction are separate actions with different service behavior.
        /// </summary>
        [Fact]
        public async Task HybridPolicy_Should_Compact_And_Evict_When_Above_Threshold()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Hybrid,
                    MaxCompletedStepsInState = 2
                }
            });

            var policy = new HybridAiExecutionRetentionPolicy(options);

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = CreateStep("step-1", AiStepExecutionStatus.Completed, -3),
                    ["step-2"] = CreateStep("step-2", AiStepExecutionStatus.Completed, -2),
                    ["step-3"] = CreateStep("step-3", AiStepExecutionStatus.Completed, -1),
                }
            };

            var decision = await policy.EvaluateAsync(state, CancellationToken.None);

            Assert.Equal(1, decision.StepsToEvict.Count);
            Assert.Equal(2, decision.StepsToCompact.Count);

            Assert.All(
                decision.StepsToEvict,
                stepName =>
                {
                    Assert.True(state.Steps.ContainsKey(stepName));
                    Assert.Equal(AiStepExecutionStatus.Completed, state.Steps[stepName].Status);
                });

            Assert.All(
                decision.StepsToCompact,
                stepName =>
                {
                    Assert.True(state.Steps.ContainsKey(stepName));
                    Assert.Equal(AiStepExecutionStatus.Completed, state.Steps[stepName].Status);
                });

            Assert.DoesNotContain(
                decision.StepsToEvict,
                stepName => decision.StepsToCompact.Contains(stepName));

            Assert.Contains("step-1", decision.StepsToEvict);
            Assert.Contains("step-2", decision.StepsToCompact);
            Assert.Contains("step-3", decision.StepsToCompact);
        }

        /// <summary>
        /// Verifies dependency-safe eviction in Evict mode.
        ///
        /// SCENARIO:
        /// - "parent" is completed.
        /// - "child" is still non-terminal and depends on "parent".
        /// - "independent-old" is completed and has no active dependent.
        ///
        /// EXPECTATION:
        /// - "parent" must not be evicted because the child still needs it.
        /// - "independent-old" can be evicted safely.
        ///
        /// WHY THIS MATTERS:
        /// - Evicting a completed parent too early can make DAG dependency resolution loop forever.
        /// </summary>
        [Fact]
        public async Task EvictPolicy_Should_Not_Evict_Completed_Parent_Required_By_NonTerminal_Child()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Evict,
                    MaxCompletedStepsInState = 1
                }
            });

            var policy = new EvictAiExecutionRetentionPolicy(options);

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["parent"] = CreateStep("parent", AiStepExecutionStatus.Completed, -3),
                    ["independent-old"] = CreateStep("independent-old", AiStepExecutionStatus.Completed, -2),

                    ["child"] = new AiStepState
                    {
                        StepName = "child",
                        Status = AiStepExecutionStatus.Ready,
                        DependsOn = new List<string> { "parent" }
                    }
                }
            };

            var decision = await policy.EvaluateAsync(state, CancellationToken.None);

            Assert.DoesNotContain("parent", decision.StepsToEvict);
            Assert.Contains("independent-old", decision.StepsToEvict);

            Assert.All(
                decision.StepsToEvict,
                stepName =>
                {
                    Assert.True(state.Steps.ContainsKey(stepName));
                    Assert.Equal(AiStepExecutionStatus.Completed, state.Steps[stepName].Status);
                });
        }

        /// <summary>
        /// Verifies dependency-safe eviction in Hybrid mode.
        ///
        /// SCENARIO:
        /// - "parent" is completed.
        /// - "child" is still non-terminal and depends on "parent".
        /// - "independent-old" is completed and has no active dependent.
        ///
        /// EXPECTATION:
        /// - "parent" must not be evicted.
        /// - "independent-old" may be evicted.
        ///
        /// WHY THIS MATTERS:
        /// - Hybrid mode also performs eviction, so it must obey the same DAG safety rule as Evict mode.
        /// </summary>
        [Fact]
        public async Task HybridPolicy_Should_Not_Evict_Completed_Parent_Required_By_NonTerminal_Child()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Hybrid,
                    MaxCompletedStepsInState = 1
                }
            });

            var policy = new HybridAiExecutionRetentionPolicy(options);

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["parent"] = CreateStep("parent", AiStepExecutionStatus.Completed, -3),
                    ["independent-old"] = CreateStep("independent-old", AiStepExecutionStatus.Completed, -2),

                    ["child"] = new AiStepState
                    {
                        StepName = "child",
                        Status = AiStepExecutionStatus.Ready,
                        DependsOn = new List<string> { "parent" }
                    }
                }
            };

            var decision = await policy.EvaluateAsync(state, CancellationToken.None);

            Assert.DoesNotContain("parent", decision.StepsToEvict);
            Assert.Contains("independent-old", decision.StepsToEvict);

            Assert.All(
                decision.StepsToEvict,
                stepName =>
                {
                    Assert.Equal(AiStepExecutionStatus.Completed, state.Steps[stepName].Status);
                });
        }

        /// <summary>
        /// Verifies that Evict mode returns an empty plan when every terminal step
        /// is still required by a non-terminal child.
        ///
        /// EXPECTATION:
        /// - No step is evicted, even if the hot state is above the configured threshold.
        ///
        /// WHY THIS MATTERS:
        /// - Safety is more important than reducing state size.
        /// - The policy must not satisfy the threshold by breaking DAG dependencies.
        /// </summary>
        [Fact]
        public async Task EvictPolicy_Should_Return_EmptyPlan_When_No_Safe_Evictable_Steps()
        {
            var options = Options.Create(new AiEngineOptions
            {
                StateRetention = new AiExecutionStateRetentionOptions
                {
                    Enabled = true,
                    Mode = AiExecutionRetentionMode.Evict,
                    MaxCompletedStepsInState = 1
                }
            });

            var policy = new EvictAiExecutionRetentionPolicy(options);

            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>
                {
                    ["parent-1"] = CreateStep("parent-1", AiStepExecutionStatus.Completed, -3),
                    ["parent-2"] = CreateStep("parent-2", AiStepExecutionStatus.Completed, -2),

                    ["child-1"] = new AiStepState
                    {
                        StepName = "child-1",
                        Status = AiStepExecutionStatus.Running,
                        DependsOn = new List<string> { "parent-1" }
                    },

                    ["child-2"] = new AiStepState
                    {
                        StepName = "child-2",
                        Status = AiStepExecutionStatus.Ready,
                        DependsOn = new List<string> { "parent-2" }
                    }
                }
            };

            var decision = await policy.EvaluateAsync(state, CancellationToken.None);

            Assert.Empty(decision.StepsToEvict);
        }

        /// <summary>
        /// Creates a single step with deterministic completion ordering.
        /// </summary>
        private static AiStepState CreateStep(
            string name,
            AiStepExecutionStatus status,
            int minutesOffset)
        {
            return new AiStepState
            {
                StepName = name,
                Status = status,
                CompletedAtUtc = status == AiStepExecutionStatus.Completed
                    ? DateTime.UtcNow.AddMinutes(minutesOffset)
                    : null
            };
        }

        /// <summary>
        /// Creates a state containing only completed steps ordered by completion time.
        /// </summary>
        private static AiExecutionState CreateStateWithCompletedSteps(int count)
        {
            var state = new AiExecutionState
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Steps = new Dictionary<string, AiStepState>()
            };

            for (var i = 0; i < count; i++)
            {
                var stepName = $"step-{i:D2}";

                state.Steps[stepName] = new AiStepState
                {
                    StepName = stepName,
                    Status = AiStepExecutionStatus.Completed,
                    CompletedAtUtc = DateTime.UtcNow.AddMinutes(-count + i)
                };
            }

            return state;
        }
    }
}
