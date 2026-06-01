using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Default execution assistance worker implementation.
    /// </summary>
    /// <remarks>
    /// This worker allows a helper runtime instance to assist an existing execution
    /// owned by another primary runtime instance.
    ///
    /// The worker does not create a new execution and does not take ownership of the
    /// local run. It only advances the existing execution identifier by reusing the
    /// standard runtime instance worker path.
    /// </remarks>
    public sealed class AiExecutionAssistanceWorker : IAiExecutionAssistanceWorker
    {
        private readonly IAiExecutionAssistanceStore _store;
        private readonly IAiRuntimeInstanceWorker _runtimeWorker;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionAssistanceWorker"/> class.
        /// </summary>
        /// <param name="store">The execution assistance lease store.</param>
        /// <param name="runtimeWorker">The runtime instance worker used to advance existing executions.</param>
        public AiExecutionAssistanceWorker(
            IAiExecutionAssistanceStore store,
            IAiRuntimeInstanceWorker runtimeWorker)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _runtimeWorker = runtimeWorker ?? throw new ArgumentNullException(nameof(runtimeWorker));
        }

        /// <inheritdoc />
        public async Task AssistAsync(
            AiExecutionAssistanceLease lease,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(lease);

            cancellationToken.ThrowIfCancellationRequested();

            if (lease.Status != AiExecutionAssistanceStatus.Granted &&
                lease.Status != AiExecutionAssistanceStatus.Active)
            {
                throw new InvalidOperationException(
                    $"Execution assistance lease '{lease.LeaseId}' cannot be used because its status is '{lease.Status}'.");
            }

            if (lease.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                await _store.UpdateStatusAsync(
                        lease.LeaseId,
                        AiExecutionAssistanceStatus.Expired,
                        "Execution assistance lease expired before helper work started.",
                        cancellationToken)
                    .ConfigureAwait(false);

                return;
            }

            await _store.UpdateStatusAsync(
                    lease.LeaseId,
                    AiExecutionAssistanceStatus.Active,
                    "Execution assistance worker started.",
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                var record = await _runtimeWorker.RunExecutionAsync(
                        lease.ExecutionId,
                        cancellationToken)
                    .ConfigureAwait(false);

                await _store.UpdateStatusAsync(
                        lease.LeaseId,
                        AiExecutionAssistanceStatus.Released,
                        $"Execution assistance worker completed with execution status '{record.Status}'.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await _store.UpdateStatusAsync(
                        lease.LeaseId,
                        AiExecutionAssistanceStatus.Revoked,
                        "Execution assistance worker was cancelled.",
                        CancellationToken.None)
                    .ConfigureAwait(false);

                throw;
            }
            catch (Exception ex)
            {
                await _store.UpdateStatusAsync(
                        lease.LeaseId,
                        AiExecutionAssistanceStatus.Failed,
                        ex.Message,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                throw;
            }
        }
    }
}