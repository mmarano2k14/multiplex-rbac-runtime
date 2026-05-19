using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Execution.Control
{
    /// <summary>
    /// Represents a request to change or evaluate the control state of an AI execution.
    /// </summary>
    public sealed class AiExecutionControlRequest
    {
        /// <summary>
        /// Gets or sets the durable execution identifier targeted by the request.
        /// </summary>
        public string ExecutionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the requested control action.
        /// </summary>
        public AiExecutionControlAction Action { get; set; } = AiExecutionControlAction.None;

        /// <summary>
        /// Gets or sets the optional reason associated with the control request.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the identity of the operator, user, service, or system that requested the action.
        /// </summary>
        public string? RequestedBy { get; set; }

        /// <summary>
        /// Gets or sets the stable waiting key used to correlate external or human input.
        /// </summary>
        public string? WaitingKey { get; set; }

        /// <summary>
        /// Gets or sets the step name associated with the control request, when applicable.
        /// </summary>
        public string? WaitingStepName { get; set; }

        /// <summary>
        /// Gets or sets input associated with the request.
        /// </summary>
        public Dictionary<string, object?> Input { get; set; } = new();
    }
}