using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.Execution;
using Multiplexed.AI.Stores.Cache;
using Multiplexed.AI.Stores.Memory;

namespace Multiplexed.AI.Stores
{
    /// <summary>
    /// Composite AI execution store.
    /// 
    /// This store uses Redis as the primary persistence layer and
    /// memory as a fallback layer for resilience.
    /// </summary>
    public sealed class AiExecutionStore : IAiExecutionStore
    {
        private readonly RedisAiExecutionStore _primary;
        private readonly MemoryAiExecutionStore _fallback;

        public AiExecutionStore(
            RedisAiExecutionStore primary,
            MemoryAiExecutionStore fallback)
        {
            ArgumentNullException.ThrowIfNull(primary);
            ArgumentNullException.ThrowIfNull(fallback);

            _primary = primary;
            _fallback = fallback;
        }

        /// <summary>
        /// Creates a new execution record and state using the primary store,
        /// and mirrors them into the fallback store.
        /// </summary>
        public async Task CreateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _primary.CreateAsync(record, state, cancellationToken);
                await _fallback.CreateAsync(record, state, cancellationToken);
            }
            catch
            {
                await _fallback.CreateAsync(record, state, cancellationToken);
            }
        }

        /// <summary>
        /// Retrieves an execution record from the primary store.
        /// Falls back to memory when the primary store is unavailable.
        /// </summary>
        public async Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var record = await _primary.GetRecordAsync(executionId, cancellationToken);

                if (record is not null)
                {
                    var state = await _primary.GetStateAsync(executionId, cancellationToken);
                    if (state is not null)
                    {
                        await _fallback.CreateAsync(record, state, cancellationToken);
                    }
                }

                return record;
            }
            catch
            {
                return await _fallback.GetRecordAsync(executionId, cancellationToken);
            }
        }

        /// <summary>
        /// Retrieves an execution state from the primary store.
        /// Falls back to memory when the primary store is unavailable.
        /// </summary>
        public async Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var state = await _primary.GetStateAsync(executionId, cancellationToken);

                if (state is not null)
                {
                    var record = await _primary.GetRecordAsync(executionId, cancellationToken);
                    if (record is not null)
                    {
                        await _fallback.CreateAsync(record, state, cancellationToken);
                    }
                }

                return state;
            }
            catch
            {
                return await _fallback.GetStateAsync(executionId, cancellationToken);
            }
        }

        /// <summary>
        /// Attempts to update an execution record and state using the primary store first.
        /// Falls back to memory if the primary store is unavailable.
        /// </summary>
        public async Task<bool> TryUpdateAsync(
            string executionId,
            string expectedStepKey,
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var updated = await _primary.TryUpdateAsync(
                    executionId,
                    expectedStepKey,
                    record,
                    state,
                    cancellationToken);

                if (updated)
                {
                    await _fallback.CreateAsync(record, state, cancellationToken);
                }

                return updated;
            }
            catch
            {
                return await _fallback.TryUpdateAsync(
                    executionId,
                    expectedStepKey,
                    record,
                    state,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Saves an execution record independently using the primary store first,
        /// then mirrors the latest known pair into the fallback store when possible.
        /// Falls back to memory if the primary store is unavailable.
        /// </summary>
        public async Task SaveRecordAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            try
            {
                await _primary.SaveRecordAsync(record, cancellationToken);

                var state = await _primary.GetStateAsync(record.ExecutionId, cancellationToken)
                    ?? await _fallback.GetStateAsync(record.ExecutionId, cancellationToken);

                if (state is not null)
                {
                    await _fallback.CreateAsync(record, state, cancellationToken);
                }
            }
            catch
            {
                var state = await _fallback.GetStateAsync(record.ExecutionId, cancellationToken)
                    ?? new AiExecutionState();

                await _fallback.CreateAsync(record, state, cancellationToken);
            }
        }

        /// <summary>
        /// Saves an execution state independently using the primary store first,
        /// then mirrors the latest known pair into the fallback store when possible.
        /// Falls back to memory if the primary store is unavailable.
        /// </summary>
        public async Task SaveStateAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(state);

            try
            {
                await _primary.SaveStateAsync(executionId, state, cancellationToken);

                var record = await _primary.GetRecordAsync(executionId, cancellationToken)
                    ?? await _fallback.GetRecordAsync(executionId, cancellationToken);

                if (record is not null)
                {
                    await _fallback.CreateAsync(record, state, cancellationToken);
                }
            }
            catch
            {
                var record = await _fallback.GetRecordAsync(executionId, cancellationToken);

                if (record is not null)
                {
                    await _fallback.CreateAsync(record, state, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Deletes an execution record from the primary store first,
        /// then deletes it from the fallback store.
        /// 
        /// This operation is idempotent and must not fail if the record
        /// is already missing in one of the stores.
        /// </summary>
        public async Task DeleteRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            Exception? primaryException = null;

            try
            {
                await _primary.DeleteRecordAsync(executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                primaryException = ex;
            }

            try
            {
                await _fallback.DeleteRecordAsync(executionId, cancellationToken);
            }
            catch
            {
                if (primaryException is not null)
                {
                    throw primaryException;
                }
            }

            if (primaryException is not null)
            {
                throw primaryException;
            }
        }

        /// <summary>
        /// Deletes an execution state from the primary store first,
        /// then deletes it from the fallback store.
        /// 
        /// This operation is idempotent and must not fail if the state
        /// is already missing in one of the stores.
        /// </summary>
        public async Task DeleteStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            Exception? primaryException = null;

            try
            {
                await _primary.DeleteStateAsync(executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                primaryException = ex;
            }

            try
            {
                await _fallback.DeleteStateAsync(executionId, cancellationToken);
            }
            catch
            {
                if (primaryException is not null)
                {
                    throw primaryException;
                }
            }

            if (primaryException is not null)
            {
                throw primaryException;
            }
        }
    }
}