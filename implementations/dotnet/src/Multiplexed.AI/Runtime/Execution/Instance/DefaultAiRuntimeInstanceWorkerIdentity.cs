using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Default logical worker identity for the runtime instance worker created by dependency injection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This identity is used for the default runtime worker resolved directly from the dependency
    /// injection container.
    /// </para>
    ///
    /// <para>
    /// Factory-created distributed workers receive their own explicit worker identities through
    /// <see cref="AiRuntimeInstanceWorkerFactory"/> and do not use this default identity.
    /// </para>
    /// </remarks>
    public sealed class DefaultAiRuntimeInstanceWorkerIdentity : IAiRuntimeInstanceWorkerIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRuntimeInstanceWorkerIdentity"/> class.
        /// </summary>
        /// <param name="runtimeInstanceIdentity">The owning runtime instance identity.</param>
        public DefaultAiRuntimeInstanceWorkerIdentity(
            IAiRuntimeInstanceIdentity runtimeInstanceIdentity)
        {
            RuntimeInstanceIdentity = runtimeInstanceIdentity
                ?? throw new ArgumentNullException(nameof(runtimeInstanceIdentity));

            WorkerId = $"{RuntimeInstanceIdentity.RuntimeInstanceId}:worker:default";
        }

        /// <inheritdoc />
        public IAiRuntimeInstanceIdentity RuntimeInstanceIdentity { get; }

        /// <inheritdoc />
        public string WorkerId { get; }
    }
}