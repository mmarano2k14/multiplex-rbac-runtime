namespace Multiplexed.Abstractions.AI.ControlPlane.Admission
{
    /// <summary>
    /// Defines the run admission controller.
    ///
    /// Admission decides whether a new run should be assigned to an available
    /// runtime instance, kept in a future shared/global queue, trigger scale-out,
    /// or be rejected according to policy.
    /// </summary>
    /// <remarks>
    /// Important:
    /// This abstraction does not enqueue the run by itself.
    /// It does not modify local queues, execute DAG steps, claim work,
    /// or create Kubernetes replicas.
    ///
    /// It only produces an admission decision based on currently visible
    /// runtime instances and admission policy.
    /// </remarks>
    public interface IAiRunAdmissionController
    {
        /// <summary>
        /// Evaluates whether a run can be admitted into the runtime control plane.
        /// </summary>
        /// <param name="request">The run admission request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The run admission decision.</returns>
        Task<AiRunAdmissionDecision> AdmitAsync(
            AiRunAdmissionRequest request,
            CancellationToken cancellationToken = default);
    }
}