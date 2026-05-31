using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Store;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;

namespace Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.SharedInstance
{
    /// <summary>
    /// Represents a request to dispatch a shared run to a specific runtime instance.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Bridges shared queue dispatch to a concrete runtime instance.
    /// - Allows a shared run claimed from the global queue to be enqueued into
    ///   the local runtime queue of the target instance.
    ///
    /// IMPORTANT:
    /// - This abstraction is transport-neutral.
    /// - In-memory mode can directly call the target local queue.
    /// - Future Kubernetes mode can route through HTTP, gRPC, Redis streams,
    ///   or another command transport.
    /// </remarks>
    public sealed class AiSharedRuntimeInstanceDispatchRequest
    {
        /// <summary>
        /// Gets the target runtime instance identifier.
        /// </summary>
        public required string RuntimeInstanceId { get; init; }

        /// <summary>
        /// Gets the shared run record to dispatch.
        /// </summary>
        public required AiSharedRunRecord SharedRun { get; init; }

        /// <summary>
        /// Gets the runtime pipeline run request to enqueue locally on the target instance.
        /// </summary>
        public required AiRuntimePipelineRunRequest RunRequest { get; init; }

        /// <summary>
        /// Gets the shared queue claim token associated with this dispatch.
        /// </summary>
        public string? ClaimToken { get; init; }

        /// <summary>
        /// Gets the correlation identifier.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Gets the user, service, worker, or controller requesting the dispatch.
        /// </summary>
        public string? RequestedBy { get; init; }

        /// <summary>
        /// Gets the source component requesting the dispatch.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// Gets the reason for dispatching the run.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the dispatch.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}