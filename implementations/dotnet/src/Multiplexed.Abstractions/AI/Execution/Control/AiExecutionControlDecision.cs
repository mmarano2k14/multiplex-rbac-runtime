namespace Multiplexed.Abstractions.AI.Execution.Control
{
    /// <summary>
    /// Represents the runtime decision produced from an execution control state check.
    /// </summary>
    public sealed class AiExecutionControlDecision
    {
        /// <summary>
        /// Gets or sets a value indicating whether the execution may continue claiming or advancing work.
        /// </summary>
        public bool CanContinue { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether workers should stop claiming new work for the execution.
        /// </summary>
        public bool ShouldStopClaiming { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether cancellation should be applied to the execution.
        /// </summary>
        public bool ShouldCancel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the execution is waiting for external or human input.
        /// </summary>
        public bool IsWaitingForInput { get; set; }

        /// <summary>
        /// Gets or sets the control status that produced this decision.
        /// </summary>
        public AiExecutionControlStatus Status { get; set; } = AiExecutionControlStatus.None;

        /// <summary>
        /// Gets or sets the optional reason associated with the decision.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Creates a decision that allows execution to continue.
        /// </summary>
        /// <returns>A control decision that allows work to continue.</returns>
        public static AiExecutionControlDecision Continue()
        {
            return new AiExecutionControlDecision
            {
                CanContinue = true,
                ShouldStopClaiming = false,
                ShouldCancel = false,
                IsWaitingForInput = false,
                Status = AiExecutionControlStatus.Running
            };
        }

        /// <summary>
        /// Creates a decision that blocks new claims without failing the execution.
        /// </summary>
        /// <param name="status">The control status causing the block.</param>
        /// <param name="reason">The optional reason for the block.</param>
        /// <returns>A control decision that stops new work from being claimed.</returns>
        public static AiExecutionControlDecision StopClaiming(
            AiExecutionControlStatus status,
            string? reason = null)
        {
            return new AiExecutionControlDecision
            {
                CanContinue = false,
                ShouldStopClaiming = true,
                ShouldCancel = false,
                IsWaitingForInput = status == AiExecutionControlStatus.WaitingForInput,
                Status = status,
                Reason = reason
            };
        }

        /// <summary>
        /// Creates a decision that requests cancellation handling.
        /// </summary>
        /// <param name="status">The control status causing cancellation.</param>
        /// <param name="reason">The optional cancellation reason.</param>
        /// <returns>A control decision that indicates cancellation should be applied.</returns>
        public static AiExecutionControlDecision Cancel(
            AiExecutionControlStatus status,
            string? reason = null)
        {
            return new AiExecutionControlDecision
            {
                CanContinue = false,
                ShouldStopClaiming = true,
                ShouldCancel = true,
                IsWaitingForInput = false,
                Status = status,
                Reason = reason
            };
        }
    }
}