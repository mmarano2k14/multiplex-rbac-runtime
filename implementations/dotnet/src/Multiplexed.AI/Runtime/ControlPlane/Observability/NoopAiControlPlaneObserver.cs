using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.Observability.Events;

namespace Multiplexed.AI.Runtime.ControlPlane.Observability
{
    /// <summary>
    /// No-op implementation of the control-plane observer.
    ///
    /// This implementation is used as a safe default when no concrete
    /// logging, metrics, tracing, or ledger exporter is registered.
    /// </summary>
    public sealed class NoopAiControlPlaneObserver : IAiControlPlaneObserver
    {
        /// <inheritdoc />
        public Task RecordAsync(
            AiControlPlaneEvent controlPlaneEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(controlPlaneEvent);

            return Task.CompletedTask;
        }
    }
}