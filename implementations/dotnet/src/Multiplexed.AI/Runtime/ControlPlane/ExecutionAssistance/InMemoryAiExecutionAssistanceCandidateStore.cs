using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;

namespace Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Provides an in-memory implementation of <see cref="IAiExecutionAssistanceCandidateStore"/>.
    /// </summary>
    /// <remarks>
    /// This store is intended for local tests and single-process simulations.
    /// A distributed implementation should be used for multi-node Kubernetes deployments.
    /// </remarks>
    public sealed class InMemoryAiExecutionAssistanceCandidateStore : IAiExecutionAssistanceCandidateStore
    {
        private readonly ConcurrentDictionary<string, AiExecutionAssistanceCandidate> _candidates =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task UpsertAsync(
            AiExecutionAssistanceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.ExecutionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.PrimaryRuntimeInstanceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.PipelineName);

            cancellationToken.ThrowIfCancellationRequested();

            _candidates.AddOrUpdate(
                candidate.ExecutionId,
                candidate,
                (_, existing) => new AiExecutionAssistanceCandidate
                {
                    ExecutionId = candidate.ExecutionId,
                    PrimaryRuntimeInstanceId = candidate.PrimaryRuntimeInstanceId,
                    LocalRunId = candidate.LocalRunId ?? existing.LocalRunId,
                    PipelineName = candidate.PipelineName,
                    PipelineVersion = candidate.PipelineVersion ?? existing.PipelineVersion,
                    EstimatedReadyStepCount = candidate.EstimatedReadyStepCount,
                    EstimatedRemainingStepCount = candidate.EstimatedRemainingStepCount,
                    EstimatedActiveWorkerCount = candidate.EstimatedActiveWorkerCount,
                    IsActive = candidate.IsActive,
                    RegisteredAtUtc = existing.RegisteredAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = candidate.CompletedAtUtc,
                    Reason = candidate.Reason ?? existing.Reason,
                    Metadata = MergeMetadata(
                        existing.Metadata,
                        candidate.Metadata)
                });

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<AiExecutionAssistanceCandidate?> GetAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            cancellationToken.ThrowIfCancellationRequested();

            _candidates.TryGetValue(
                executionId,
                out var candidate);

            return Task.FromResult(candidate);
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<AiExecutionAssistanceCandidate>> ListActiveAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = _candidates.Values
                .Where(candidate => candidate.IsActive)
                .OrderBy(candidate => candidate.RegisteredAtUtc)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<AiExecutionAssistanceCandidate>>(candidates);
        }

        /// <inheritdoc />
        public Task MarkCompletedAsync(
            string executionId,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            cancellationToken.ThrowIfCancellationRequested();

            _candidates.AddOrUpdate(
                executionId,
                _ => throw new InvalidOperationException(
                    $"Execution assistance candidate '{executionId}' was not found."),
                (_, existing) => new AiExecutionAssistanceCandidate
                {
                    ExecutionId = existing.ExecutionId,
                    PrimaryRuntimeInstanceId = existing.PrimaryRuntimeInstanceId,
                    LocalRunId = existing.LocalRunId,
                    PipelineName = existing.PipelineName,
                    PipelineVersion = existing.PipelineVersion,
                    EstimatedReadyStepCount = existing.EstimatedReadyStepCount,
                    EstimatedRemainingStepCount = existing.EstimatedRemainingStepCount,
                    EstimatedActiveWorkerCount = existing.EstimatedActiveWorkerCount,
                    IsActive = false,
                    RegisteredAtUtc = existing.RegisteredAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Reason = reason ?? existing.Reason,
                    Metadata = existing.Metadata
                });

            return Task.CompletedTask;
        }

        private static IReadOnlyDictionary<string, string> MergeMetadata(
            IReadOnlyDictionary<string, string> existing,
            IReadOnlyDictionary<string, string> updated)
        {
            var metadata = new Dictionary<string, string>(
                existing,
                StringComparer.Ordinal);

            foreach (var item in updated)
            {
                metadata[item.Key] = item.Value;
            }

            return metadata;
        }
    }
}