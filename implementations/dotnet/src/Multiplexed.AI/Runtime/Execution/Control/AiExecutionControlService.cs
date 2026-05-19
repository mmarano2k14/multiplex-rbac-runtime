using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution.Control;

namespace Multiplexed.AI.Runtime.Execution.Control
{
    /// <summary>
    /// Provides high-level durable control operations for AI executions.
    /// </summary>
    /// <remarks>
    /// This service owns execution control transitions such as pause, resume,
    /// cancellation, and waiting for external input. It does not own DAG execution
    /// state and must remain separate from step state, payload state, retry state,
    /// and replay state.
    /// </remarks>
    public sealed class AiExecutionControlService : IAiExecutionControlService
    {
        private const int MaxUpdateAttempts = 8;

        private readonly IAiExecutionControlStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionControlService"/> class.
        /// </summary>
        /// <param name="store">The durable execution control store.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="store"/> is null.
        /// </exception>
        public AiExecutionControlService(IAiExecutionControlStore store)
        {
            ArgumentNullException.ThrowIfNull(store);

            _store = store;
        }

        /// <inheritdoc />
        public Task<AiExecutionControlState> PauseExecutionAsync(
            string executionId,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            return ApplyTransitionAsync(
                executionId,
                existing => ApplyPause(existing, executionId, reason, requestedBy),
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<AiExecutionControlState> ResumeExecutionAsync(
            string executionId,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            return ApplyTransitionAsync(
                executionId,
                existing => ApplyResume(existing, executionId, requestedBy),
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<AiExecutionControlState> CancelExecutionAsync(
            string executionId,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            return ApplyTransitionAsync(
                executionId,
                existing => ApplyCancel(existing, executionId, reason, requestedBy),
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<AiExecutionControlState> MarkWaitingForInputAsync(
            string executionId,
            string waitingKey,
            string? waitingStepName = null,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            if (string.IsNullOrWhiteSpace(waitingKey))
            {
                throw new ArgumentException("Waiting key cannot be null, empty, or whitespace.", nameof(waitingKey));
            }

            return ApplyTransitionAsync(
                executionId,
                existing => ApplyWaitingForInput(
                    existing,
                    executionId,
                    waitingKey,
                    waitingStepName,
                    reason,
                    requestedBy),
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<AiExecutionControlState> SubmitHumanInputAsync(
            string executionId,
            string waitingKey,
            IReadOnlyDictionary<string, object?> input,
            string? submittedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            if (string.IsNullOrWhiteSpace(waitingKey))
            {
                throw new ArgumentException("Waiting key cannot be null, empty, or whitespace.", nameof(waitingKey));
            }

            ArgumentNullException.ThrowIfNull(input);

            return ApplyTransitionAsync(
                executionId,
                existing => ApplyHumanInput(existing, executionId, waitingKey, input, submittedBy),
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlDecision> CheckCanAdvanceAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            var state = await _store.GetAsync(executionId, cancellationToken).ConfigureAwait(false);

            if (state is null)
            {
                return AiExecutionControlDecision.Continue();
            }

            return state.Status switch
            {
                AiExecutionControlStatus.None => AiExecutionControlDecision.Continue(),
                AiExecutionControlStatus.Running => AiExecutionControlDecision.Continue(),
                AiExecutionControlStatus.Resuming => AiExecutionControlDecision.Continue(),

                AiExecutionControlStatus.Pausing => AiExecutionControlDecision.StopClaiming(
                    state.Status,
                    state.Reason),

                AiExecutionControlStatus.Paused => AiExecutionControlDecision.StopClaiming(
                    state.Status,
                    state.Reason),

                AiExecutionControlStatus.WaitingForInput => AiExecutionControlDecision.StopClaiming(
                    state.Status,
                    state.Reason),

                AiExecutionControlStatus.Cancelling => AiExecutionControlDecision.Cancel(
                    state.Status,
                    state.Reason),

                AiExecutionControlStatus.Cancelled => AiExecutionControlDecision.Cancel(
                    state.Status,
                    state.Reason),

                _ => AiExecutionControlDecision.StopClaiming(
                    state.Status,
                    state.Reason)
            };
        }

        private async Task<AiExecutionControlState> ApplyTransitionAsync(
            string executionId,
            Func<AiExecutionControlState?, AiExecutionControlState> transition,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < MaxUpdateAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existing = await _store.GetAsync(executionId, cancellationToken).ConfigureAwait(false);
                var next = transition(existing);

                if (existing is null)
                {
                    next.Version = 1;
                    next.UpdatedAtUtc = DateTime.UtcNow;

                    var created = await _store.TryCreateAsync(next, cancellationToken).ConfigureAwait(false);

                    if (created)
                    {
                        return next;
                    }

                    continue;
                }

                var expectedVersion = existing.Version;
                next.Version = expectedVersion + 1;
                next.UpdatedAtUtc = DateTime.UtcNow;

                var updated = await _store.TryUpdateAsync(next, expectedVersion, cancellationToken)
                    .ConfigureAwait(false);

                if (updated)
                {
                    return next;
                }
            }

            throw new InvalidOperationException(
                $"Unable to update execution control state for execution '{executionId}' after {MaxUpdateAttempts} attempts.");
        }

        private static AiExecutionControlState ApplyPause(
            AiExecutionControlState? existing,
            string executionId,
            string? reason,
            string? requestedBy)
        {
            var state = CloneOrCreate(existing, executionId);

            if (state.Status is AiExecutionControlStatus.Cancelled or AiExecutionControlStatus.Cancelling)
            {
                return state;
            }

            if (state.Status == AiExecutionControlStatus.Paused &&
                state.PendingAction == AiExecutionControlAction.None)
            {
                return state;
            }

            state.Status = AiExecutionControlStatus.Pausing;
            state.PendingAction = AiExecutionControlAction.Pause;
            state.Reason = reason;
            state.RequestedBy = requestedBy;
            state.PauseRequestedAtUtc ??= DateTime.UtcNow;

            return state;
        }

        private static AiExecutionControlState ApplyResume(
            AiExecutionControlState? existing,
            string executionId,
            string? requestedBy)
        {
            var state = CloneOrCreate(existing, executionId);

            if (state.Status is AiExecutionControlStatus.Cancelled or AiExecutionControlStatus.Cancelling)
            {
                return state;
            }

            state.Status = AiExecutionControlStatus.Resuming;
            state.PendingAction = AiExecutionControlAction.Resume;
            state.RequestedBy = requestedBy;
            state.ResumeRequestedAtUtc = DateTime.UtcNow;

            return state;
        }

        private static AiExecutionControlState ApplyCancel(
            AiExecutionControlState? existing,
            string executionId,
            string? reason,
            string? requestedBy)
        {
            var state = CloneOrCreate(existing, executionId);

            if (state.Status == AiExecutionControlStatus.Cancelled)
            {
                return state;
            }

            state.Status = AiExecutionControlStatus.Cancelling;
            state.PendingAction = AiExecutionControlAction.Cancel;
            state.Reason = reason;
            state.RequestedBy = requestedBy;
            state.CancellationRequestedAtUtc ??= DateTime.UtcNow;

            return state;
        }

        private static AiExecutionControlState ApplyWaitingForInput(
            AiExecutionControlState? existing,
            string executionId,
            string waitingKey,
            string? waitingStepName,
            string? reason,
            string? requestedBy)
        {
            var state = CloneOrCreate(existing, executionId);

            if (state.Status is AiExecutionControlStatus.Cancelled or AiExecutionControlStatus.Cancelling)
            {
                return state;
            }

            state.Status = AiExecutionControlStatus.WaitingForInput;
            state.PendingAction = AiExecutionControlAction.WaitForInput;
            state.WaitingKey = waitingKey;
            state.WaitingStepName = waitingStepName;
            state.Reason = reason;
            state.RequestedBy = requestedBy;
            state.WaitingStartedAtUtc ??= DateTime.UtcNow;

            return state;
        }

        private static AiExecutionControlState ApplyHumanInput(
            AiExecutionControlState? existing,
            string executionId,
            string waitingKey,
            IReadOnlyDictionary<string, object?> input,
            string? submittedBy)
        {
            var state = CloneOrCreate(existing, executionId);

            if (state.Status is AiExecutionControlStatus.Cancelled or AiExecutionControlStatus.Cancelling)
            {
                return state;
            }

            if (!string.Equals(state.WaitingKey, waitingKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Execution '{executionId}' is not waiting for input key '{waitingKey}'.");
            }

            state.Input = new Dictionary<string, object?>(input, StringComparer.Ordinal);
            state.Status = AiExecutionControlStatus.Resuming;
            state.PendingAction = AiExecutionControlAction.SubmitInput;
            state.RequestedBy = submittedBy;
            state.InputReceivedAtUtc = DateTime.UtcNow;

            return state;
        }

        private static AiExecutionControlState CloneOrCreate(
            AiExecutionControlState? existing,
            string executionId)
        {
            if (existing is null)
            {
                return new AiExecutionControlState
                {
                    ExecutionId = executionId
                };
            }

            return new AiExecutionControlState
            {
                ExecutionId = existing.ExecutionId,
                Status = existing.Status,
                PendingAction = existing.PendingAction,
                Reason = existing.Reason,
                RequestedBy = existing.RequestedBy,
                WaitingKey = existing.WaitingKey,
                WaitingStepName = existing.WaitingStepName,
                Input = new Dictionary<string, object?>(existing.Input, StringComparer.Ordinal),
                Version = existing.Version,
                UpdatedAtUtc = existing.UpdatedAtUtc,
                PauseRequestedAtUtc = existing.PauseRequestedAtUtc,
                PausedAtUtc = existing.PausedAtUtc,
                ResumeRequestedAtUtc = existing.ResumeRequestedAtUtc,
                CancellationRequestedAtUtc = existing.CancellationRequestedAtUtc,
                CancelledAtUtc = existing.CancelledAtUtc,
                WaitingStartedAtUtc = existing.WaitingStartedAtUtc,
                InputReceivedAtUtc = existing.InputReceivedAtUtc
            };
        }

        private static void ValidateExecutionId(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null, empty, or whitespace.", nameof(executionId));
            }
        }
    }
}