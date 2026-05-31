using Multiplexed.Abstractions.AI.ControlPlane.Observability.Events;

namespace Multiplexed.AI.Runtime.Observability.Logging
{
    /// <summary>
    /// Defines structured logging for AI control-plane events.
    /// </summary>
    public interface IAiControlPlaneLogger
    {
        /// <summary>
        /// Logs a structured control-plane event.
        /// </summary>
        /// <param name="controlPlaneEvent">The control-plane event to log.</param>
        void LogControlPlaneEvent(
            AiControlPlaneEvent controlPlaneEvent);
    }
}