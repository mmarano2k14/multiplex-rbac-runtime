using Multiplexed.Abstractions.AI.ControlPlane.RuntimeQueue;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;

namespace Multiplexed.AI.Runtime.ControlPlane.SharedController
{
    /// <summary>
    /// Local implementation of the shared run dispatcher.
    /// </summary>
    /// <remarks>
    /// This dispatcher sends a shared run to the local runtime queue through
    /// <see cref="IAiRuntimeQueueControlPlane"/>.
    ///
    /// V1 is intentionally local-only.
    /// It does not call remote pods.
    /// It does not use Kubernetes.
    /// It does not perform scaling.
    /// It does not execute DAG steps directly.
    /// </remarks>
    public sealed class LocalAiSharedRunDispatcher : IAiSharedRunDispatcher
    {
        private readonly IAiRuntimeQueueControlPlane _runtimeQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalAiSharedRunDispatcher"/> class.
        /// </summary>
        /// <param name="runtimeQueue">The local runtime queue control-plane facade.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="runtimeQueue"/> is null.
        /// </exception>
        public LocalAiSharedRunDispatcher(
            IAiRuntimeQueueControlPlane runtimeQueue)
        {
            _runtimeQueue = runtimeQueue ?? throw new ArgumentNullException(nameof(runtimeQueue));
        }

        /// <inheritdoc />
        public async Task<AiSharedRunDispatchResult> DispatchAsync(
            AiSharedRunDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.SharedRun);
            ArgumentNullException.ThrowIfNull(request.SharedRun.RunRequest);

            if (string.IsNullOrWhiteSpace(request.SharedRun.SharedRunId))
            {
                throw new ArgumentException(
                    "Shared run id cannot be null or empty.",
                    nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.RuntimeInstanceId))
            {
                throw new ArgumentException(
                    "Runtime instance id cannot be null or empty.",
                    nameof(request));
            }

            var startedAtUtc = DateTimeOffset.UtcNow;

            try
            {
                var queueResult = await _runtimeQueue
                    .EnqueueRunAsync(
                        new AiRuntimeQueueControlPlaneRequest
                        {
                            Operation = AiRuntimeQueueControlPlaneOperation.EnqueueRun,
                            RunRequest = request.SharedRun.RunRequest,
                            CorrelationId = request.CorrelationId ?? request.SharedRun.CorrelationId,
                            RequestedBy = request.RequestedBy,
                            Source = request.Source,
                            Reason = request.Reason,
                            Metadata = MergeMetadata(
                                request.SharedRun.Metadata,
                                request.Metadata)
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                var completedAtUtc = DateTimeOffset.UtcNow;

                if (!queueResult.Success)
                {
                    return new AiSharedRunDispatchResult
                    {
                        Success = false,
                        SharedRunId = request.SharedRun.SharedRunId,
                        RuntimeInstanceId = request.RuntimeInstanceId,
                        ClaimToken = request.ClaimToken,
                        Message = "Shared run dispatch failed.",
                        FailureReason = queueResult.FailureReason ?? queueResult.Message,
                        StartedAtUtc = startedAtUtc,
                        CompletedAtUtc = completedAtUtc,
                        DurationMs = CalculateDurationMs(startedAtUtc, completedAtUtc),
                        Diagnostics = queueResult.Diagnostics
                    };
                }

                return new AiSharedRunDispatchResult
                {
                    Success = true,
                    SharedRunId = request.SharedRun.SharedRunId,
                    RuntimeInstanceId = request.RuntimeInstanceId,
                    LocalRunId = queueResult.RunHandle?.RunId,
                    ExecutionId = queueResult.RunHandle?.ExecutionId,
                    ClaimToken = request.ClaimToken,
                    Message = "Shared run dispatched to local runtime queue.",
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = CalculateDurationMs(startedAtUtc, completedAtUtc),
                    Diagnostics = queueResult.Diagnostics
                };
            }
            catch (Exception exception)
            {
                var completedAtUtc = DateTimeOffset.UtcNow;

                return new AiSharedRunDispatchResult
                {
                    Success = false,
                    SharedRunId = request.SharedRun.SharedRunId,
                    RuntimeInstanceId = request.RuntimeInstanceId,
                    ClaimToken = request.ClaimToken,
                    Message = "Shared run dispatch failed.",
                    FailureReason = exception.Message,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = CalculateDurationMs(startedAtUtc, completedAtUtc),
                    Diagnostics = new[] { exception.Message }
                };
            }
        }

        /// <summary>
        /// Calculates dispatch duration in milliseconds.
        /// </summary>
        /// <param name="startedAtUtc">The dispatch start timestamp.</param>
        /// <param name="completedAtUtc">The dispatch completion timestamp.</param>
        /// <returns>The duration in milliseconds.</returns>
        private static long CalculateDurationMs(
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            return (long)(completedAtUtc - startedAtUtc).TotalMilliseconds;
        }

        /// <summary>
        /// Merges metadata dictionaries.
        /// </summary>
        /// <param name="baseMetadata">The base metadata.</param>
        /// <param name="overrideMetadata">The override metadata.</param>
        /// <returns>The merged metadata.</returns>
        private static IReadOnlyDictionary<string, string> MergeMetadata(
            IReadOnlyDictionary<string, string> baseMetadata,
            IReadOnlyDictionary<string, string> overrideMetadata)
        {
            var merged = new Dictionary<string, string>(
                baseMetadata,
                StringComparer.Ordinal);

            foreach (var pair in overrideMetadata)
            {
                merged[pair.Key] = pair.Value;
            }

            return merged;
        }
    }
}