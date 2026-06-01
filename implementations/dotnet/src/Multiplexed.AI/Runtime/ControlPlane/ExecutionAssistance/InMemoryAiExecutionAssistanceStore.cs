using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;

namespace Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Provides an in-memory implementation of <see cref="IAiExecutionAssistanceStore"/>.
    /// </summary>
    /// <remarks>
    /// This store is intended for local execution, tests, and non-distributed simulations.
    /// A Redis-backed implementation should be used for real multi-node Kubernetes deployments.
    /// </remarks>
    public sealed class InMemoryAiExecutionAssistanceStore : IAiExecutionAssistanceStore
    {
        private readonly ConcurrentDictionary<string, AiExecutionAssistanceLease> _leases =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task RegisterAsync(
            AiExecutionAssistanceLease lease,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(lease);

            cancellationToken.ThrowIfCancellationRequested();

            _leases[lease.LeaseId] = lease;

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<AiExecutionAssistanceLease?> GetAsync(
            string leaseId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);

            cancellationToken.ThrowIfCancellationRequested();

            _leases.TryGetValue(
                leaseId,
                out var lease);

            return Task.FromResult(lease);
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<AiExecutionAssistanceLease>> ListByExecutionAsync(
            string executionId,
            bool includeTerminal = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            cancellationToken.ThrowIfCancellationRequested();

            var leases = _leases.Values
                .Where(lease =>
                    string.Equals(
                        lease.ExecutionId,
                        executionId,
                        StringComparison.Ordinal) &&
                    (includeTerminal || !IsTerminal(lease.Status)))
                .OrderBy(lease => lease.GrantedAtUtc)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<AiExecutionAssistanceLease>>(leases);
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<AiExecutionAssistanceLease>> ListByHelperAsync(
            string helperRuntimeInstanceId,
            bool includeTerminal = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(helperRuntimeInstanceId);

            cancellationToken.ThrowIfCancellationRequested();

            var leases = _leases.Values
                .Where(lease =>
                    string.Equals(
                        lease.HelperRuntimeInstanceId,
                        helperRuntimeInstanceId,
                        StringComparison.Ordinal) &&
                    (includeTerminal || !IsTerminal(lease.Status)))
                .OrderBy(lease => lease.GrantedAtUtc)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<AiExecutionAssistanceLease>>(leases);
        }

        /// <inheritdoc />
        public Task UpdateStatusAsync(
            string leaseId,
            AiExecutionAssistanceStatus status,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);

            cancellationToken.ThrowIfCancellationRequested();

            _leases.AddOrUpdate(
                leaseId,
                _ => throw new InvalidOperationException(
                    $"Execution assistance lease '{leaseId}' was not found."),
                (_, existing) => new AiExecutionAssistanceLease
                {
                    LeaseId = existing.LeaseId,
                    ExecutionId = existing.ExecutionId,
                    PrimaryRuntimeInstanceId = existing.PrimaryRuntimeInstanceId,
                    HelperRuntimeInstanceId = existing.HelperRuntimeInstanceId,
                    MaxWorkers = existing.MaxWorkers,
                    Status = status,
                    GrantedAtUtc = existing.GrantedAtUtc,
                    ExpiresAtUtc = existing.ExpiresAtUtc,
                    StartedAtUtc = status == AiExecutionAssistanceStatus.Active &&
                                   existing.StartedAtUtc is null
                        ? DateTimeOffset.UtcNow
                        : existing.StartedAtUtc,
                    CompletedAtUtc = IsTerminal(status)
                        ? DateTimeOffset.UtcNow
                        : existing.CompletedAtUtc,
                    Reason = reason ?? existing.Reason,
                    Metadata = existing.Metadata
                });

            return Task.CompletedTask;
        }

        private static bool IsTerminal(
            AiExecutionAssistanceStatus status)
        {
            return status == AiExecutionAssistanceStatus.Released ||
                   status == AiExecutionAssistanceStatus.Expired ||
                   status == AiExecutionAssistanceStatus.Revoked ||
                   status == AiExecutionAssistanceStatus.Failed;
        }
    }
}