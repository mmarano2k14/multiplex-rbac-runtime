using Multiplexed.Abstractions.AI.ControlPlane.Observability.Events;

namespace Multiplexed.Abstractions.AI.ControlPlane.Observability
{
    /// <summary>
    /// Observes control-plane operations and exports structured events to
    /// logs, metrics, tracing, ledger, and future dashboard pipelines.
    ///
    /// Implementations must remain adapter-neutral and must not depend directly
    /// on Kibana, Grafana, ASP.NET, MCP, or Kubernetes.
    /// </summary>
    public interface IAiControlPlaneObserver
    {
        /// <summary>
        /// Records a structured control-plane event.
        /// </summary>
        Task RecordAsync(
            AiControlPlaneEvent controlPlaneEvent,
            CancellationToken cancellationToken = default);
    }
}