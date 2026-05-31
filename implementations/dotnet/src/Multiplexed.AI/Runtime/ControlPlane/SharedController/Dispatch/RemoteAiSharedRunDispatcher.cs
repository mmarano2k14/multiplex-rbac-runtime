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

            Console.WriteLine(
                $"[REMOTE-DISPATCH][START] SharedRunId={request.SharedRun.SharedRunId}, RuntimeInstanceId={request.RuntimeInstanceId}, ClaimToken={request.ClaimToken}, CorrelationId={request.CorrelationId ?? request.SharedRun.CorrelationId}");

            if (request.SharedRun.RunRequest is null)
            {
                var completedAtUtc = DateTimeOffset.UtcNow;

                Console.WriteLine(
                    $"[REMOTE-DISPATCH][FAIL] Missing RunRequest. SharedRunId={request.SharedRun.SharedRunId}, RuntimeInstanceId={request.RuntimeInstanceId}");

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

            var registeredInstances = await _runtimeInstanceRegistry
                .ListAsync(cancellationToken)
                .ConfigureAwait(false);

            Console.WriteLine(
                $"[REMOTE-DISPATCH][REGISTRY] Target={request.RuntimeInstanceId}, Registered=[{string.Join(", ", registeredInstances.Select(instance => instance.RuntimeInstanceId))}]");

            var runtimeInstance = await _runtimeInstanceRegistry
                .GetAsync(
                    request.RuntimeInstanceId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (runtimeInstance is null)
            {
                var completedAtUtc = DateTimeOffset.UtcNow;

                Console.WriteLine(
                    $"[REMOTE-DISPATCH][FAIL] Runtime instance not registered. SharedRunId={request.SharedRun.SharedRunId}, RuntimeInstanceId={request.RuntimeInstanceId}");

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

            Console.WriteLine(
                $"[REMOTE-DISPATCH][RESOLVED] SharedRunId={request.SharedRun.SharedRunId}, Target={request.RuntimeInstanceId}, Resolved={runtimeInstance.RuntimeInstanceId}");

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

                Console.WriteLine(
                    $"[REMOTE-DISPATCH][EXCEPTION] SharedRunId={request.SharedRun.SharedRunId}, RuntimeInstanceId={request.RuntimeInstanceId}, ExceptionType={exception.GetType().Name}, Message={exception.Message}");

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

            Console.WriteLine(
                $"[REMOTE-DISPATCH][RESULT] Success={instanceResult.Success}, SharedRunId={instanceResult.SharedRunId ?? request.SharedRun.SharedRunId}, RuntimeInstanceId={request.RuntimeInstanceId}, LocalRunId={instanceResult.LocalRunId}, ExecutionId={instanceResult.ExecutionId}, FailureReason={instanceResult.FailureReason}, DurationMs={durationMs}");

            if (instanceResult.Metadata.Count > 0)
            {
                Console.WriteLine(
                    $"[REMOTE-DISPATCH][RESULT-METADATA] {FormatMetadata(instanceResult.Metadata)}");
            }

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

        private static string FormatMetadata(
            IReadOnlyDictionary<string, string> metadata)
        {
            return string.Join(
                " | ",
                metadata
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => $"{item.Key}={item.Value}"));
        }
    }
}