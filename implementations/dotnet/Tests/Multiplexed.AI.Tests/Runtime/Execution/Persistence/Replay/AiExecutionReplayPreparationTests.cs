using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution.Persistence.Replay;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.Runtime.Execution.Persistence.Replay
{
    /// <summary>
    /// Verifies replay preparation behavior before a persisted execution snapshot
    /// is restored into the runtime store.
    ///
    /// These tests protect the replay semantics from regression by ensuring that:
    /// - in-flight runtime state is converted into replay-safe state
    /// - transient claim fields are cleared
    /// - terminal states are preserved
    /// - record-level runtime projections are reset when replay resumes execution
    /// </summary>
    public sealed class AiExecutionReplayPreparationTests
    {
        /// <summary>
        /// Verifies that a running step is converted back to Ready
        /// so that replay never restores an in-flight step as actively running.
        /// </summary>
        [Fact]
        public void Prepare_Should_Convert_Running_Step_To_Ready()
        {
            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1",
                PipelineName = "pipeline-1",
                Status = AiExecutionStatus.Running
            };

            var step = new AiStepState
            {
                StepName = "step-1",
                Status = AiStepExecutionStatus.Running
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = step
                }
            };

            AiExecutionReplayPreparation.Prepare(record, state);

            Assert.Equal(AiStepExecutionStatus.Ready, step.Status);
        }

        /// <summary>
        /// Verifies that transient claim-related fields are always cleared
        /// during replay preparation.
        ///
        /// A replay must never restore stale worker ownership or stale claims.
        /// </summary>
        [Fact]
        public void Prepare_Should_Clear_Transient_Claim_Fields()
        {
            var step = new AiStepState
            {
                StepName = "step-1",
                Status = AiStepExecutionStatus.Running,
                ClaimToken = "claim-token",
                ClaimedBy = "worker-1",
                ClaimedAtUtc = DateTime.UtcNow
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = step
                }
            };

            AiExecutionReplayPreparation.Prepare(
                new AiExecutionRecord
                {
                    ExecutionId = "exec-1",
                    PipelineName = "pipeline-1",
                    Status = AiExecutionStatus.Running
                },
                state);

            Assert.Null(step.ClaimToken);
            Assert.Null(step.ClaimedBy);
            Assert.Null(step.ClaimedAtUtc);
        }

        /// <summary>
        /// Verifies that a completed step remains completed.
        /// Replay preparation must not downgrade terminal completed work.
        /// </summary>
        [Fact]
        public void Prepare_Should_Preserve_Completed_Step()
        {
            var step = new AiStepState
            {
                StepName = "step-1",
                Status = AiStepExecutionStatus.Completed
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = step
                }
            };

            AiExecutionReplayPreparation.Prepare(
                new AiExecutionRecord
                {
                    ExecutionId = "exec-1",
                    PipelineName = "pipeline-1",
                    Status = AiExecutionStatus.Running
                },
                state);

            Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
        }

        /// <summary>
        /// Verifies that a failed step remains failed.
        /// Replay preparation should not silently rewrite terminal failure state.
        /// </summary>
        [Fact]
        public void Prepare_Should_Preserve_Failed_Step()
        {
            var step = new AiStepState
            {
                StepName = "step-1",
                Status = AiStepExecutionStatus.Failed
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = step
                }
            };

            AiExecutionReplayPreparation.Prepare(
                new AiExecutionRecord
                {
                    ExecutionId = "exec-1",
                    PipelineName = "pipeline-1",
                    Status = AiExecutionStatus.Running
                },
                state);

            Assert.Equal(AiStepExecutionStatus.Failed, step.Status);
        }

        /// <summary>
        /// Verifies that a waiting-for-retry step remains waiting-for-retry.
        /// Replay preparation must preserve retry scheduling semantics.
        /// </summary>
        [Fact]
        public void Prepare_Should_Preserve_WaitingForRetry_Step()
        {
            var step = new AiStepState
            {
                StepName = "step-1",
                Status = AiStepExecutionStatus.WaitingForRetry
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>
                {
                    ["step-1"] = step
                }
            };

            AiExecutionReplayPreparation.Prepare(
                new AiExecutionRecord
                {
                    ExecutionId = "exec-1",
                    PipelineName = "pipeline-1",
                    Status = AiExecutionStatus.Running
                },
                state);

            Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step.Status);
        }

        /// <summary>
        /// Verifies that a non-terminal execution record is normalized back to Running
        /// and that transient record-level runtime projection fields are cleared.
        /// </summary>
        [Fact]
        public void Prepare_Should_Reset_Record_Runtime_Projection_For_NonTerminal_Record()
        {
            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1",
                PipelineName = "pipeline-1",
                Status = AiExecutionStatus.Running,
                CurrentStep = "step-3",
                ExecutionStepKey = "step-key-3"
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            AiExecutionReplayPreparation.Prepare(record, state);

            Assert.Equal(AiExecutionStatus.Running, record.Status);
            Assert.Null(record.CurrentStep);
            Assert.Null(record.ExecutionStepKey);
        }

        /// <summary>
        /// Verifies that terminal record status is preserved.
        /// Replay preparation must not rewrite completed, failed, or cancelled executions.
        /// </summary>
        [Theory]
        [InlineData(AiExecutionStatus.Completed)]
        [InlineData(AiExecutionStatus.Failed)]
        [InlineData(AiExecutionStatus.Cancelled)]
        public void Prepare_Should_Not_Modify_Terminal_Record_Status(AiExecutionStatus terminalStatus)
        {
            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1",
                PipelineName = "pipeline-1",
                Status = terminalStatus,
                CurrentStep = "step-3",
                ExecutionStepKey = "step-key-3"
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            AiExecutionReplayPreparation.Prepare(record, state);

            Assert.Equal(terminalStatus, record.Status);
        }

        /// <summary>
        /// Verifies that null inputs are safely tolerated.
        /// Replay preparation is intentionally defensive and best-effort.
        /// </summary>
        [Fact]
        public void Prepare_Should_Tolerate_Null_Record_And_State()
        {
            AiExecutionReplayPreparation.Prepare(null, null);
        }

        /// <summary>
        /// Verifies that replay preparation tolerates an execution state
        /// that has no steps to normalize.
        /// </summary>
        [Fact]
        public void Prepare_Should_Tolerate_State_With_No_Steps()
        {
            var record = new AiExecutionRecord
            {
                ExecutionId = "exec-1",
                PipelineName = "pipeline-1",
                Status = AiExecutionStatus.Running
            };

            var state = new AiExecutionState
            {
                ExecutionId = "exec-1",
                Steps = new Dictionary<string, AiStepState>()
            };

            AiExecutionReplayPreparation.Prepare(record, state);

            Assert.Equal(AiExecutionStatus.Running, record.Status);
        }
    }
}