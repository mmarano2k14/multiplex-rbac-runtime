using System;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Unit tests covering the local state-machine invariants of <see cref="AiStepState"/>.
    ///
    /// PURPOSE:
    /// - Validate legal transitions
    /// - Reject illegal transitions early
    /// - Protect retry / recovery / claim semantics from accidental regression
    ///
    /// IMPORTANT:
    /// These tests validate only the step-level state machine.
    /// They do not cover:
    /// - distributed Redis/Lua atomicity
    /// - execution-level convergence
    /// - engine orchestration
    /// </summary>
    public sealed class AiStepStateInvariantTests
    {
        /// <summary>
        /// Ensures a step cannot be claimed twice.
        /// </summary>
        [Fact]
        public void MarkRunning_Should_Throw_When_Step_Is_Already_Running()
        {
            var step = CreateStep();
            step.MarkRunning("worker-1", "token-1");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkRunning("worker-2", "token-2"));

            Assert.Contains("already running", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a terminal step cannot be claimed again.
        /// </summary>
        [Theory]
        [InlineData(AiStepExecutionStatus.Completed)]
        [InlineData(AiStepExecutionStatus.Failed)]
        public void MarkRunning_Should_Throw_When_Step_Is_Terminal(AiStepExecutionStatus terminalStatus)
        {
            var step = CreateStep();
            step.Status = terminalStatus;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkRunning("worker-1", "token-1"));

            Assert.Contains("terminal", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures completion is only legal from Running.
        /// </summary>
        [Theory]
        [InlineData(AiStepExecutionStatus.None)]
        [InlineData(AiStepExecutionStatus.Ready)]
        [InlineData(AiStepExecutionStatus.WaitingForRetry)]
        [InlineData(AiStepExecutionStatus.Completed)]
        [InlineData(AiStepExecutionStatus.Failed)]
        public void MarkCompleted_Should_Throw_When_Step_Is_Not_Running(AiStepExecutionStatus invalidStatus)
        {
            var step = CreateStep();
            step.Status = invalidStatus;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkCompleted(new AiStepResult()));

            Assert.Contains("cannot complete", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures failure is only legal from Running or WaitingForRetry.
        /// </summary>
        [Theory]
        [InlineData(AiStepExecutionStatus.None)]
        [InlineData(AiStepExecutionStatus.Ready)]
        [InlineData(AiStepExecutionStatus.Completed)]
        [InlineData(AiStepExecutionStatus.Failed)]
        public void MarkFailed_Should_Throw_When_Step_Is_Not_Running_Or_WaitingForRetry(AiStepExecutionStatus invalidStatus)
        {
            var step = CreateStep();
            step.Status = invalidStatus;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkFailed("boom"));

            Assert.Contains("cannot fail", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures retry waiting is only legal from Running.
        /// </summary>
        [Theory]
        [InlineData(AiStepExecutionStatus.None)]
        [InlineData(AiStepExecutionStatus.Ready)]
        [InlineData(AiStepExecutionStatus.WaitingForRetry)]
        [InlineData(AiStepExecutionStatus.Completed)]
        [InlineData(AiStepExecutionStatus.Failed)]
        public void MarkWaitingForRetry_Should_Throw_When_Step_Is_Not_Running(AiStepExecutionStatus invalidStatus)
        {
            var step = CreateStep();
            step.Status = invalidStatus;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkWaitingForRetry("retry later", DateTime.UtcNow.AddSeconds(10)));

            Assert.Contains("WaitingForRetry", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures retry waiting rejects invalid retry state where RetryCount already exceeds MaxRetries.
        /// </summary>
        [Fact]
        public void MarkWaitingForRetry_Should_Throw_When_RetryCount_Exceeds_MaxRetries()
        {
            var step = CreateRunningStep();
            step.RetryCount = 4;
            step.MaxRetries = 3;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkWaitingForRetry("retry later", DateTime.UtcNow.AddSeconds(10)));

            Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures timeout recovery is only legal from Running.
        /// </summary>
        [Theory]
        [InlineData(AiStepExecutionStatus.None)]
        [InlineData(AiStepExecutionStatus.Ready)]
        [InlineData(AiStepExecutionStatus.WaitingForRetry)]
        [InlineData(AiStepExecutionStatus.Completed)]
        [InlineData(AiStepExecutionStatus.Failed)]
        public void MarkRequeuedAfterTimeout_Should_Throw_When_Step_Is_Not_Running(AiStepExecutionStatus invalidStatus)
        {
            var step = CreateStep();
            step.Status = invalidStatus;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkRequeuedAfterTimeout());

            Assert.Contains("cannot be recovered", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures retry-or-fail is only legal from Running.
        /// </summary>
        [Theory]
        [InlineData(AiStepExecutionStatus.None)]
        [InlineData(AiStepExecutionStatus.Ready)]
        [InlineData(AiStepExecutionStatus.WaitingForRetry)]
        [InlineData(AiStepExecutionStatus.Completed)]
        [InlineData(AiStepExecutionStatus.Failed)]
        public void MarkRetryOrFail_Should_Throw_When_Step_Is_Not_Running(AiStepExecutionStatus invalidStatus)
        {
            var step = CreateStep();
            step.Status = invalidStatus;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkRetryOrFail("error", DateTime.UtcNow));

            Assert.Contains("RetryOrFail", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures retry-or-fail rejects impossible retry state where RetryCount already exceeds MaxRetries.
        /// </summary>
        [Fact]
        public void MarkRetryOrFail_Should_Throw_When_RetryCount_Already_Exceeds_MaxRetries()
        {
            var step = CreateRunningStep();
            step.RetryCount = 5;
            step.MaxRetries = 3;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                step.MarkRetryOrFail("error", DateTime.UtcNow));

            Assert.Contains("RetryCount", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures retry-or-fail increments RetryCount and moves to WaitingForRetry when budget remains.
        /// </summary>
        [Fact]
        public void MarkRetryOrFail_Should_Increment_Retry_And_Transition_To_WaitingForRetry_When_Budget_Remains()
        {
            var step = CreateRunningStep();
            step.RetryCount = 0;
            step.MaxRetries = 2;
            step.RetryDelayMs = 1000;

            step.MarkRetryOrFail("error", DateTime.UtcNow);

            Assert.Equal(AiStepExecutionStatus.WaitingForRetry, step.Status);
            Assert.Equal(1, step.RetryCount);
            Assert.NotNull(step.NextRetryAtUtc);
            Assert.Null(step.ClaimedBy);
            Assert.Null(step.ClaimToken);
            Assert.Null(step.ClaimedAtUtc);
            Assert.Null(step.LeaseExpiresAtUtc);
        }

        /// <summary>
        /// Ensures retry-or-fail transitions to terminal Failed when retry budget is exhausted.
        /// </summary>
        [Fact]
        public void MarkRetryOrFail_Should_Transition_To_Failed_When_Retry_Budget_Is_Exhausted()
        {
            var step = CreateRunningStep();
            step.RetryCount = 2;
            step.MaxRetries = 2;

            step.MarkRetryOrFail("fatal", DateTime.UtcNow);

            Assert.Equal(AiStepExecutionStatus.Failed, step.Status);
            Assert.Equal(2, step.RetryCount);
            Assert.Null(step.ClaimedBy);
            Assert.Null(step.ClaimToken);
            Assert.Null(step.ClaimedAtUtc);
            Assert.Null(step.LeaseExpiresAtUtc);
        }

        /// <summary>
        /// Ensures MarkCompleted clears all claim metadata.
        /// </summary>
        [Fact]
        public void MarkCompleted_Should_Clear_Claim_Metadata()
        {
            var step = CreateRunningStep();

            step.MarkCompleted(new AiStepResult());

            Assert.Equal(AiStepExecutionStatus.Completed, step.Status);
            Assert.Null(step.ClaimedBy);
            Assert.Null(step.ClaimToken);
            Assert.Null(step.ClaimedAtUtc);
            Assert.Null(step.LeaseExpiresAtUtc);
        }

        /// <summary>
        /// Ensures timeout recovery does not modify RetryCount.
        /// </summary>
        [Fact]
        public void MarkRequeuedAfterTimeout_Should_Not_Modify_RetryCount()
        {
            var step = CreateRunningStep();
            step.RetryCount = 2;
            step.RecoveryCount = 0;

            step.MarkRequeuedAfterTimeout();

            Assert.Equal(AiStepExecutionStatus.Ready, step.Status);
            Assert.Equal(2, step.RetryCount);
            Assert.Equal(1, step.RecoveryCount);
        }

        /// <summary>
        /// Ensures a running claim computes lease expiration when ClaimTimeoutSeconds is configured.
        /// </summary>
        [Fact]
        public void MarkRunning_Should_Set_Lease_When_Timeout_Is_Configured()
        {
            var step = CreateStep();
            step.ClaimTimeoutSeconds = 30;

            step.MarkRunning("worker-1", "token-1");

            Assert.Equal(AiStepExecutionStatus.Running, step.Status);
            Assert.Equal("worker-1", step.ClaimedBy);
            Assert.Equal("token-1", step.ClaimToken);
            Assert.NotNull(step.ClaimedAtUtc);
            Assert.NotNull(step.LeaseExpiresAtUtc);
            Assert.True(step.LeaseExpiresAtUtc > step.ClaimedAtUtc);
        }

        /// <summary>
        /// Ensures lease expiration is left null when no timeout is configured.
        /// </summary>
        [Fact]
        public void MarkRunning_Should_Not_Set_Lease_When_Timeout_Is_Not_Configured()
        {
            var step = CreateStep();
            step.ClaimTimeoutSeconds = null;

            step.MarkRunning("worker-1", "token-1");

            Assert.Equal(AiStepExecutionStatus.Running, step.Status);
            Assert.Null(step.LeaseExpiresAtUtc);
        }

        // ---------------------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------------------

        private static AiStepState CreateStep()
        {
            return new AiStepState
            {
                StepName = "step-1",
                Status = AiStepExecutionStatus.Ready,
                MaxRetries = 3,
                RetryDelayMs = 1000
            };
        }

        private static AiStepState CreateRunningStep()
        {
            var step = CreateStep();
            step.ClaimTimeoutSeconds = 30;
            step.MarkRunning("worker-1", "token-1");
            return step;
        }
    }
}