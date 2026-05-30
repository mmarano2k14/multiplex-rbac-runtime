namespace Multiplexed.Abstractions.AI.ControlPlane.Admission
{
    /// <summary>
    /// Defines the high-level outcome of a run admission decision.
    /// </summary>
    public enum AiRunAdmissionDecisionType
    {
        /// <summary>
        /// No admission decision could be made.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The run can be assigned to a selected runtime instance.
        /// </summary>
        AssignToInstance = 1,

        /// <summary>
        /// No instance is currently available, but the run may remain pending
        /// in a shared/global queue.
        /// </summary>
        QueueGlobally = 2,

        /// <summary>
        /// No instance is currently available and scale-out should be requested.
        /// </summary>
        RequestScaleOut = 3,

        /// <summary>
        /// The run should be rejected according to the admission policy.
        /// </summary>
        Reject = 4
    }
}