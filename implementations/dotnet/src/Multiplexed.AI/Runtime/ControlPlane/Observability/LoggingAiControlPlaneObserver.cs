using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.AI.Runtime.Observability.Logging;

namespace Multiplexed.AI.Runtime.ControlPlane.Observability
{
    /// <summary>
    /// Control-plane observer that forwards structured control-plane events
    /// to the runtime logging layer.
    /// </summary>
    public sealed class LoggedAiControlPlaneObserver : IAiControlPlaneObserver
    {
        private readonly IAiControlPlaneLogger _logger;

        public LoggedAiControlPlaneObserver(
            IAiControlPlaneLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task RecordAsync(
            AiControlPlaneEvent controlPlaneEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(controlPlaneEvent);

            _logger.LogControlPlaneEvent(controlPlaneEvent);

            return Task.CompletedTask;
        }
    }
}