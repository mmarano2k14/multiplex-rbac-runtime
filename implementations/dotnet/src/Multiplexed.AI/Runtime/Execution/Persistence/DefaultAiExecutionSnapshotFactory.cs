using System;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;

namespace Multiplexed.AI.Runtime.Persistence
{
    /// <summary>
    /// Default implementation of <see cref="IAiExecutionSnapshotFactory{TContextSnapshot}"/>.
    ///
    /// This factory converts the current runtime execution models into a durable
    /// snapshot document suitable for persistence, inspection, audit, and
    /// post-mortem debugging.
    ///
    /// Design notes:
    /// - The runtime keeps durable step state in <see cref="AiExecutionState.Steps"/>
    ///   as a dictionary keyed by logical step name
    /// - The snapshot stores step states as a flat list for simpler document persistence
    /// - Technical execution events are initialized empty and can be appended later
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The serializable external context snapshot type associated with the execution.
    /// </typeparam>
    public sealed class DefaultAiExecutionSnapshotFactory<TContextSnapshot> : IAiExecutionSnapshotFactory<TContextSnapshot>
    {
        /// <inheritdoc />
        public AiExecutionSnapshotDocument<TContextSnapshot> Create(
            AiExecutionRecord record,
            AiExecutionState state,
            string? contextKey,
            TContextSnapshot? contextSnapshot)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            var stepStates = state.Steps.Values.ToList();

            return new AiExecutionSnapshotDocument<TContextSnapshot>
            {
                ExecutionId = record.ExecutionId,
                PipelineName = record.PipelineName ?? throw new InvalidOperationException( $"Execution '{record.ExecutionId}' has no PipelineName."),
                Status = record.Status.ToString(),
                ContextKey = contextKey,
                ContextSnapshot = contextSnapshot,
                CreatedAtUtc = record.CreatedAtUtc,
                UpdatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = record.CompletedAtUtc,
                Record = record,
                State = state,
                Steps = stepStates,
                Events = new List<AiExecutionEvent>()
            };
        }
    }
}