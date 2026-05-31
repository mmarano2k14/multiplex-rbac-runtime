using Multiplexed.Abstractions.AI.ControlPlane.SharedController;

namespace Multiplexed.AI.Runtime.ControlPlane.SharedController
{
    /// <summary>
    /// No-op implementation of <see cref="IAiRuntimeScaleOutRequestPublisher"/>.
    /// </summary>
    /// <remarks>
    /// This implementation acknowledges scale-out requests without creating
    /// infrastructure.
    ///
    /// It is the default implementation for local mode, tests, and demos where
    /// Kubernetes or external scaling adapters are not configured yet.
    /// </remarks>
    public sealed class NoopAiRuntimeScaleOutRequestPublisher : IAiRuntimeScaleOutRequestPublisher
    {
        /// <inheritdoc />
        public Task<AiRuntimeScaleOutRequestResult> PublishAsync(
            AiRuntimeScaleOutRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SharedRunId);

            var target = request.MaxInstanceCount.HasValue
                ? Math.Min(request.CurrentInstanceCount + 1, request.MaxInstanceCount.Value)
                : request.CurrentInstanceCount + 1;

            return Task.FromResult(new AiRuntimeScaleOutRequestResult
            {
                Success = true,
                SharedRunId = request.SharedRunId,
                ScaleOutRequestId = $"noop-scale-out-{request.SharedRunId}",
                RequestedTargetInstanceCount = target,
                Message = "Scale-out request acknowledged by no-op publisher.",
                PublishedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }
}