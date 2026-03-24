using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution;

namespace Multiplexed.AI.Stores.Memory
{
    /// <summary>
    /// In-memory fallback store for AI execution records and states.
    /// 
    /// This store is intended for resilience and temporary fallback scenarios.
    /// It is not a durable persistence mechanism.
    /// </summary>
    public sealed class MemoryAiExecutionStore : IAiExecutionStore
    {
        private readonly ConcurrentDictionary<string, AiExecutionRecord> _records = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, AiExecutionState> _states = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates a new execution record and state in memory.
        /// </summary>
        public Task CreateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            _records[record.ExecutionId] = CloneRecord(record);
            _states[state.ExecutionId] = CloneState(state);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves an execution record from memory.
        /// </summary>
        public Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            _records.TryGetValue(executionId, out var record);

            return Task.FromResult(record is null ? null : CloneRecord(record));
        }

        /// <summary>
        /// Retrieves an execution state from memory.
        /// </summary>
        public Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            _states.TryGetValue(executionId, out var state);

            return Task.FromResult(state is null ? null : CloneState(state));
        }

        /// <summary>
        /// Attempts to update an execution record and state using optimistic concurrency.
        /// </summary>
        public Task<bool> TryUpdateAsync(
            string executionId,
            string expectedStepKey,
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            if (string.IsNullOrWhiteSpace(expectedStepKey))
                throw new ArgumentException("Expected step key cannot be null or empty.", nameof(expectedStepKey));

            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (!_records.TryGetValue(executionId, out var currentRecord))
                return Task.FromResult(false);

            if (!string.Equals(currentRecord.ExecutionStepKey, expectedStepKey, StringComparison.Ordinal))
                return Task.FromResult(false);

            _records[executionId] = CloneRecord(record);
            _states[executionId] = CloneState(state);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Creates a defensive copy of the execution record.
        /// </summary>
        private static AiExecutionRecord CloneRecord(AiExecutionRecord source)
        {
            return new AiExecutionRecord
            {
                ExecutionId = source.ExecutionId,
                ContextKey = source.ContextKey,
                CurrentStepIndex = source.CurrentStepIndex,
                Steps = new List<string>(source.Steps),
                CompletedSteps = new List<string>(source.CompletedSteps),
                ExecutionContextSnapshot = source.ExecutionContextSnapshot,
                Status = source.Status,
                Version = source.Version,
                CreatedAtUtc = source.CreatedAtUtc,
                UpdatedAtUtc = source.UpdatedAtUtc,
                CurrentStep = source.CurrentStep,
                ExecutionStepKey = source.ExecutionStepKey
            };
        }

        /// <summary>
        /// Creates a defensive copy of the execution state.
        /// </summary>
        private static AiExecutionState CloneState(AiExecutionState source)
        {
            return new AiExecutionState
            {
                ExecutionId = source.ExecutionId,
                Data = new Dictionary<string, object?>(source.Data, StringComparer.Ordinal),
                Metadata = new Dictionary<string, object?>(source.Metadata, StringComparer.Ordinal),
                CreatedAtUtc = source.CreatedAtUtc,
                UpdatedAtUtc = source.UpdatedAtUtc
            };
        }
    }
}