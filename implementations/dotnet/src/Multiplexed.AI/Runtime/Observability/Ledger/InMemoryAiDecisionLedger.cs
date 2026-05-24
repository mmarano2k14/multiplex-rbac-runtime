using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Observability.Ledger;

namespace Multiplexed.AI.Observability.Ledger
{
    /// <summary>
    /// Provides an in-memory append-only implementation of <see cref="IAiDecisionLedger"/>.
    /// </summary>
    /// <remarks>
    /// This implementation is intended for tests, local demos, and development scenarios.
    /// It is not a durable production ledger. Production persistence should use a durable
    /// store such as MongoDB.
    /// </remarks>
    public sealed class InMemoryAiDecisionLedger : IAiDecisionLedger
    {
        private readonly object _syncRoot = new();
        private readonly List<AiDecisionLedgerEntry> _entries = new();
        private readonly Dictionary<string, long> _sequencesByExecution = new(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task AppendAsync(
            AiDecisionLedgerEntry entry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.EntryId);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.ExecutionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.EventType);

            cancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                var sequence = GetNextSequence(entry.ExecutionId);

                var storedEntry = new AiDecisionLedgerEntry
                {
                    EntryId = entry.EntryId,
                    ExecutionId = entry.ExecutionId,
                    Sequence = sequence,
                    Category = entry.Category,
                    EventType = entry.EventType,
                    Outcome = entry.Outcome,
                    TimestampUtc = entry.TimestampUtc,
                    RunId = entry.RunId,
                    StepId = entry.StepId,
                    StepKey = entry.StepKey,
                    PipelineName = entry.PipelineName,
                    PipelineVersion = entry.PipelineVersion,
                    RuntimeInstanceId = entry.RuntimeInstanceId,
                    WorkerId = entry.WorkerId,
                    PolicyKey = entry.PolicyKey,
                    Provider = entry.Provider,
                    Model = entry.Model,
                    Operation = entry.Operation,
                    Reason = entry.Reason,
                    CorrelationId = entry.CorrelationId,
                    TraceId = entry.TraceId,
                    ClaimToken = entry.ClaimToken,
                    InputPayloadRef = entry.InputPayloadRef,
                    OutputPayloadRef = entry.OutputPayloadRef,
                    Metadata = entry.Metadata
                };

                _entries.Add(storedEntry);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiDecisionLedgerEntry>> GetByExecutionAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<AiDecisionLedgerEntry> result;

            lock (_syncRoot)
            {
                result = _entries
                    .Where(entry => string.Equals(entry.ExecutionId, executionId, StringComparison.Ordinal))
                    .OrderBy(entry => entry.Sequence)
                    .ThenBy(entry => entry.TimestampUtc)
                    .ToArray();
            }

            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AiDecisionLedgerEntry>> QueryAsync(
            AiDecisionLedgerQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<AiDecisionLedgerEntry> result;

            lock (_syncRoot)
            {
                var entries = _entries.AsEnumerable();

                entries = ApplyFilters(entries, query);

                entries = entries
                    .OrderBy(entry => entry.ExecutionId)
                    .ThenBy(entry => entry.Sequence)
                    .ThenBy(entry => entry.TimestampUtc);

                if (query.Limit is > 0)
                {
                    entries = entries.Take(query.Limit.Value);
                }

                result = entries.ToArray();
            }

            return Task.FromResult(result);
        }

        private long GetNextSequence(string executionId)
        {
            if (!_sequencesByExecution.TryGetValue(executionId, out var currentSequence))
            {
                currentSequence = 0;
            }

            var nextSequence = currentSequence + 1;

            _sequencesByExecution[executionId] = nextSequence;

            return nextSequence;
        }

        private static IEnumerable<AiDecisionLedgerEntry> ApplyFilters(
            IEnumerable<AiDecisionLedgerEntry> entries,
            AiDecisionLedgerQuery query)
        {
            if (!string.IsNullOrWhiteSpace(query.ExecutionId))
            {
                entries = entries.Where(entry => string.Equals(entry.ExecutionId, query.ExecutionId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.RunId))
            {
                entries = entries.Where(entry => string.Equals(entry.RunId, query.RunId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.StepId))
            {
                entries = entries.Where(entry => string.Equals(entry.StepId, query.StepId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.StepKey))
            {
                entries = entries.Where(entry => string.Equals(entry.StepKey, query.StepKey, StringComparison.Ordinal));
            }

            if (query.Category.HasValue)
            {
                entries = entries.Where(entry => entry.Category == query.Category.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.EventType))
            {
                entries = entries.Where(entry => string.Equals(entry.EventType, query.EventType, StringComparison.Ordinal));
            }

            if (query.Outcome.HasValue)
            {
                entries = entries.Where(entry => entry.Outcome == query.Outcome.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.RuntimeInstanceId))
            {
                entries = entries.Where(entry => string.Equals(entry.RuntimeInstanceId, query.RuntimeInstanceId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.WorkerId))
            {
                entries = entries.Where(entry => string.Equals(entry.WorkerId, query.WorkerId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.PolicyKey))
            {
                entries = entries.Where(entry => string.Equals(entry.PolicyKey, query.PolicyKey, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.Provider))
            {
                entries = entries.Where(entry => string.Equals(entry.Provider, query.Provider, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.Model))
            {
                entries = entries.Where(entry => string.Equals(entry.Model, query.Model, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.Operation))
            {
                entries = entries.Where(entry => string.Equals(entry.Operation, query.Operation, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            {
                entries = entries.Where(entry => string.Equals(entry.CorrelationId, query.CorrelationId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.TraceId))
            {
                entries = entries.Where(entry => string.Equals(entry.TraceId, query.TraceId, StringComparison.Ordinal));
            }

            if (query.SequenceFrom.HasValue)
            {
                entries = entries.Where(entry => entry.Sequence >= query.SequenceFrom.Value);
            }

            if (query.SequenceTo.HasValue)
            {
                entries = entries.Where(entry => entry.Sequence <= query.SequenceTo.Value);
            }

            if (query.TimestampFromUtc.HasValue)
            {
                entries = entries.Where(entry => entry.TimestampUtc >= query.TimestampFromUtc.Value);
            }

            if (query.TimestampToUtc.HasValue)
            {
                entries = entries.Where(entry => entry.TimestampUtc <= query.TimestampToUtc.Value);
            }

            return entries;
        }
    }
}