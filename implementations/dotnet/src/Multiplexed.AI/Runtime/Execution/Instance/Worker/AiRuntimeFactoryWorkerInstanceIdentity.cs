using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Runtime instance identity used for factory-created distributed workers.
    /// </summary>
    internal sealed class AiRuntimeFactoryWorkerInstanceIdentity : IAiRuntimeInstanceIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeFactoryWorkerInstanceIdentity"/> class.
        /// </summary>
        /// <param name="runtimeInstanceId">The runtime instance identifier.</param>
        public AiRuntimeFactoryWorkerInstanceIdentity(
            string runtimeInstanceId)
        {
            RuntimeInstanceId = runtimeInstanceId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceId));
        }

        /// <inheritdoc />
        public string RuntimeInstanceId { get; }

        /// <inheritdoc />
        public string HostName { get; } = Environment.MachineName;

        /// <inheritdoc />
        public int ProcessId { get; } = Environment.ProcessId;

        /// <inheritdoc />
        public DateTimeOffset StartedAtUtc { get; } =
            DateTimeOffset.UtcNow;
    }
}