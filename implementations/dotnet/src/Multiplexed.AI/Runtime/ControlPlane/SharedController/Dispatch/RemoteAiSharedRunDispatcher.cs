using Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.SharedInstance;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Dispatch;

namespace Multiplexed.AI.Runtime.ControlPlane.SharedController.Dispatch
{
    /// <summary>
    /// Dispatches shared runs to addressable shared runtime instances.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Bridges the shared queue / shared controller layer to concrete runtime instances.
    /// - Resolves a target runtime instance from <see cref="IAiSharedRuntimeInstanceRegistry"/>.
    /// - Dispatches the shared run into the selected runtime instance.
    ///
    /// WHY THIS EXISTS:
    /// - <c>LocalAiSharedRunDispatcher</c> dispatches only into the local process.
    /// - This dispatcher allows a shared queue pump to route work to a specific runtime instance.
    ///
    /// KUBERNETES DIRECTION:
    /// - In-memory registry can be used for tests and local multi-instance simulation.
    /// - Future implementations can resolve runtime instances backed by HTTP, gRPC,
    ///   Redis command queues, or Kubernetes service discovery.
    /// </remarks>
    public sealed class RemoteAiSharedRunDispatcher : IAiSharedRunDispatcher
    {
        private readonly IAiSharedRuntimeInstanceRegistry _runtimeInstanceRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteAiSharedRunDispatcher"/> class.
        /// </summary>
        /// <param name="runtimeInstanceRegistry">
        /// Registry used to resolve a runtime instance id to a dispatchable shared runtime instance.
        /// </param>
        public RemoteAiSharedRunDispatcher(
            IAiSharedRuntimeInstanceRegistry runtimeInstanceRegistry)
        {
            ArgumentNullException.ThrowIfNull(runtimeInstanceRegistry);

            _runtimeInstanceRegistry = runtimeInstanceRegistry;
        }

        /// <inheritdoc />
        public async Task<AiSharedRunDispatchResult> DispatchAsync(
            AiSharedRunDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.RuntimeInstanceId);
            ArgumentNullException.ThrowIfNull(request.SharedRun);

            var startedAtUtc = DateTimeOffset.UtcNow;

            if (request.SharedRun.RunRequest is null)
            {
                var completedAtUtc = DateTimeOffset.UtcNow;

                return new AiSharedRunDispatchResult
                {
                    Success = false,
                    SharedRunId = request.SharedRun.SharedRunId,
                    RuntimeInstanceId = request.RuntimeInstanceId,
                    ClaimToken = request.ClaimToken,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = (long)(completedAtUtc - startedAtUtc).TotalMilliseconds,
                    FailureReason = "Shared run does not contain a runtime pipeline run request.",
                    Metadata = CreateFailureMetadata(
                        request,
                        "missing-run-request")
                };
            }

            var runtimeInstance = await _runtimeInstanceRegistry
                .GetAsync(
                    request.RuntimeInstanceId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (runtimeInstance is null)
            {
                var completedAtUtc = DateTimeOffset.UtcNow;

                return new AiSharedRunDispatchResult
                {
                    Success = false,
                    SharedRunId = request.SharedRun.SharedRunId,
                    RuntimeInstanceId = request.RuntimeInstanceId,
                    ClaimToken = request.ClaimToken,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = (long)(completedAtUtc - startedAtUtc).TotalMilliseconds,
                    FailureReason =
                        $"Runtime instance '{request.RuntimeInstanceId}' is not registered as a shared runtime instance.",
                    Metadata = CreateFailureMetadata(
                        request,
                        "runtime-instance-not-registered")
                };
            }

            var dispatchMetadata = MergeMetadata(
                request.Metadata,
                request.SharedRun.Metadata,
                request.SharedRun.SharedRunId,
                request.RuntimeInstanceId,
                request.ClaimToken);

            AiSharedRuntimeInstanceDispatchResult instanceResult;

            try
            {
                instanceResult = await runtimeInstance
                    .DispatchAsync(
                        new AiSharedRuntimeInstanceDispatchRequest
                        {
                            RuntimeInstanceId = request.RuntimeInstanceId,
                            SharedRun = request.SharedRun,
                            RunRequest = request.SharedRun.RunRequest,
                            ClaimToken = request.ClaimToken,
                            CorrelationId =
                                request.CorrelationId ??
                                request.SharedRun.CorrelationId,
                            RequestedBy = request.RequestedBy,
                            Source = request.Source,
                            Reason = request.Reason,
                            Metadata = dispatchMetadata
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var failedAtUtc = DateTimeOffset.UtcNow;

                return new AiSharedRunDispatchResult
                {
                    Success = false,
                    SharedRunId = request.SharedRun.SharedRunId,
                    RuntimeInstanceId = request.RuntimeInstanceId,
                    ClaimToken = request.ClaimToken,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = failedAtUtc,
                    DurationMs = (long)(failedAtUtc - startedAtUtc).TotalMilliseconds,
                    FailureReason = exception.Message,
                    Metadata = CreateFailureMetadata(
                        request,
                        "exception",
                        exception)
                };
            }

            var completedAtUtcFinal = DateTimeOffset.UtcNow;
            var durationMs = (long)(completedAtUtcFinal - startedAtUtc).TotalMilliseconds;

            var resultMetadata = MergeResultMetadata(
                dispatchMetadata,
                instanceResult.Metadata,
                instanceResult.LocalRunId,
                instanceResult.ExecutionId,
                instanceResult.Success,
                instanceResult.FailureReason);

            return new AiSharedRunDispatchResult
            {
                Success = instanceResult.Success,
                SharedRunId =
                    instanceResult.SharedRunId ??
                    request.SharedRun.SharedRunId,
                RuntimeInstanceId = request.RuntimeInstanceId,
                LocalRunId = instanceResult.LocalRunId,
                ExecutionId = instanceResult.ExecutionId,
                ClaimToken =
                    instanceResult.ClaimToken ??
                    request.ClaimToken,
                Message = instanceResult.Message,
                FailureReason = instanceResult.FailureReason,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtcFinal,
                DurationMs = durationMs,
                Metadata = resultMetadata
            };
        }

        /// <summary>
        /// Merges dispatch metadata and shared run metadata into a single dictionary.
        /// </summary>
        /// <param name="dispatchMetadata">Metadata provided by the dispatch request.</param>
        /// <param name="sharedRunMetadata">Metadata stored on the shared run record.</param>
        /// <param name="sharedRunId">The shared run identifier.</param>
        /// <param name="runtimeInstanceId">The target runtime instance identifier.</param>
        /// <param name="claimToken">The shared queue claim token.</param>
        /// <returns>The merged metadata dictionary.</returns>
        private static IReadOnlyDictionary<string, string> MergeMetadata(
            IReadOnlyDictionary<string, string>? dispatchMetadata,
            IReadOnlyDictionary<string, string>? sharedRunMetadata,
            string sharedRunId,
            string runtimeInstanceId,
            string? claimToken)
        {
            var metadata = new Dictionary<string, string>(
                StringComparer.Ordinal);

            if (sharedRunMetadata is not null)
            {
                foreach (var item in sharedRunMetadata)
                {
                    metadata[item.Key] = item.Value;
                }
            }

            if (dispatchMetadata is not null)
            {
                foreach (var item in dispatchMetadata)
                {
                    metadata[item.Key] = item.Value;
                }
            }

            metadata["shared.run.id"] = sharedRunId;
            metadata["runtime.instance.id"] = runtimeInstanceId;
            metadata["remote.dispatch"] = "true";

            if (!string.IsNullOrWhiteSpace(claimToken))
            {
                metadata["claim.token"] = claimToken;
            }

            return metadata;
        }

        /// <summary>
        /// Merges remote dispatch metadata with metadata returned by the target runtime instance.
        /// </summary>
        /// <param name="dispatchMetadata">Metadata prepared before calling the target runtime instance.</param>
        /// <param name="instanceMetadata">Metadata returned by the target runtime instance.</param>
        /// <param name="localRunId">The local run id created by the target runtime instance.</param>
        /// <param name="executionId">The execution id returned by the target runtime instance, when available.</param>
        /// <param name="success">Whether the runtime instance dispatch succeeded.</param>
        /// <param name="failureReason">The failure reason returned by the runtime instance, when available.</param>
        /// <returns>The merged result metadata dictionary.</returns>
        private static IReadOnlyDictionary<string, string> MergeResultMetadata(
            IReadOnlyDictionary<string, string> dispatchMetadata,
            IReadOnlyDictionary<string, string>? instanceMetadata,
            string? localRunId,
            string? executionId,
            bool success,
            string? failureReason)
        {
            var metadata = new Dictionary<string, string>(
                dispatchMetadata,
                StringComparer.Ordinal);

            if (instanceMetadata is not null)
            {
                foreach (var item in instanceMetadata)
                {
                    metadata[item.Key] = item.Value;
                }
            }

            metadata["remote.dispatch.success"] = success.ToString();
            metadata["remote.dispatch.local.run.id"] = localRunId ?? string.Empty;
            metadata["remote.dispatch.execution.id"] = executionId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                metadata["remote.dispatch.failure.reason"] = failureReason;
            }

            return metadata;
        }

        /// <summary>
        /// Creates metadata for a failed remote dispatch operation.
        /// </summary>
        /// <param name="request">The original shared run dispatch request.</param>
        /// <param name="failureCode">A stable failure code describing the failure category.</param>
        /// <param name="exception">The exception that caused the failure, when available.</param>
        /// <returns>The failure metadata dictionary.</returns>
        private static IReadOnlyDictionary<string, string> CreateFailureMetadata(
            AiSharedRunDispatchRequest request,
            string failureCode,
            Exception? exception = null)
        {
            var metadata = MergeMetadata(
                request.Metadata,
                request.SharedRun.Metadata,
                request.SharedRun.SharedRunId,
                request.RuntimeInstanceId,
                request.ClaimToken);

            var result = new Dictionary<string, string>(
                metadata,
                StringComparer.Ordinal)
            {
                ["remote.dispatch.success"] = "False",
                ["remote.dispatch.failure.code"] = failureCode
            };

            if (exception is not null)
            {
                result["remote.dispatch.exception.type"] =
                    exception.GetType().FullName ?? exception.GetType().Name;
            }

            return result;
        }
    }
}