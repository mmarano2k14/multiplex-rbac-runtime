namespace Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Evaluates whether idle runtime instances may assist active executions
    /// owned by other primary runtime instances.
    /// </summary>
    public interface IAiExecutionAssistanceController
    {
        /// <summary>
        /// Evaluates whether a candidate helper runtime instance can assist an execution.
        /// </summary>
        /// <param name="request">The assistance evaluation request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The assistance decision.</returns>
        Task<AiExecutionAssistanceDecision> EvaluateAsync(
            AiExecutionAssistanceRequest request,
            CancellationToken cancellationToken = default);
    }
}