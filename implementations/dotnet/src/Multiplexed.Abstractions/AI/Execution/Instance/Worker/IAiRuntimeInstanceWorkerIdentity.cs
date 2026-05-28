using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;

namespace Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Provides identity information for a logical runtime worker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A runtime worker identity combines the owning runtime instance identity with
    /// a logical worker identifier.
    /// </para>
    ///
    /// <para>
    /// The runtime instance identity represents the host, process, pod, or runtime
    /// participant. The worker identifier represents the logical worker running inside
    /// that runtime instance.
    /// </para>
    ///
    /// <para>
    /// Multiple workers may cooperate on the same execution identifier, but each worker
    /// must have its own worker identifier for metrics, tracing, leases, diagnostics,
    /// and correlation.
    /// </para>
    /// </remarks>
    public interface IAiRuntimeInstanceWorkerIdentity
    {
        /// <summary>
        /// Gets the owning runtime instance identity.
        /// </summary>
        IAiRuntimeInstanceIdentity RuntimeInstanceIdentity { get; }

        /// <summary>
        /// Gets the logical worker identifier.
        /// </summary>
        string WorkerId { get; }
    }
}