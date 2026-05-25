using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution.Control;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.AI.Runtime.Observability.Helpers;

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
        private const string ControlPipelineKey = "execution-control";
        private const string ControlScope = "_control";
        private const string HumanInputScope = "_human_input";

        private readonly IAiExecutionControlStore _store;
        private readonly IAiRuntimeObservability? _observability;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionControlService"/> class.
        /// </summary>
        /// <param name="store">The durable execution control store.</param>
        /// <param name="observability">The runtime observability facade.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="store"/> is null.
        /// </exception>
        public AiExecutionControlService(
            IAiExecutionControlStore store,
            IAiRuntimeObservability observability)
            : this(store)
        {
            _observability = observability;
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlState> PauseExecutionAsync(
            string executionId,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            var state = await ApplyTransitionAsync(
                    executionId,
                    existing => ApplyPause(existing, executionId, reason, requestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordControlLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.Control.PauseRequested,
                    AiDecisionLedgerOutcome.Applied,
                    reason ?? "Execution pause requested.",
                    CreateControlMetadata(state),
                    cancellationToken)
                .ConfigureAwait(false);

            return state;
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlState> ResumeExecutionAsync(
            string executionId,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            var state = await ApplyTransitionAsync(
                    executionId,
                    existing => ApplyResume(existing, executionId, requestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordControlLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.Control.ResumeRequested,
                    AiDecisionLedgerOutcome.Applied,
                    "Execution resume requested.",
                    CreateControlMetadata(state),
                    cancellationToken)
                .ConfigureAwait(false);

            return state;
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlState> CancelExecutionAsync(
            string executionId,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            var state = await ApplyTransitionAsync(
                    executionId,
                    existing => ApplyCancel(existing, executionId, reason, requestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordControlLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.Control.CancelRequested,
                    AiDecisionLedgerOutcome.Applied,
                    reason ?? "Execution cancellation requested.",
                    CreateControlMetadata(state),
                    cancellationToken)
                .ConfigureAwait(false);

            return state;
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlState> MarkWaitingForInputAsync(
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

            var state = await ApplyTransitionAsync(
                    executionId,
                    existing => ApplyWaitingForInput(
                        existing,
                        executionId,
                        waitingKey,
                        waitingStepName,
                        reason,
                        requestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            var metadata = CreateHumanInputMetadata(state);

            await RecordHumanInputLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.HumanInput.Requested,
                    AiDecisionLedgerOutcome.Applied,
                    reason ?? "Human input requested.",
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordHumanInputLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.HumanInput.Waiting,
                    AiDecisionLedgerOutcome.Applied,
                    reason ?? "Execution is waiting for human input.",
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);

            return state;
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlState> SubmitHumanInputAsync(
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

            var state = await ApplyTransitionAsync(
                    executionId,
                    existing => ApplyHumanInput(existing, executionId, waitingKey, input, submittedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordHumanInputLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.HumanInput.Submitted,
                    AiDecisionLedgerOutcome.Applied,
                    "Human input submitted.",
                    CreateHumanInputMetadata(state),
                    cancellationToken)
                .ConfigureAwait(false);

            return state;
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
                AiExecutionControlStatus.None => AiExecutionControlDecision.Continue(
                    AiExecutionControlStatus.Running,
                    state.Reason),

                AiExecutionControlStatus.Running => AiExecutionControlDecision.Continue(
                    state.Status,
                    state.Reason),

                AiExecutionControlStatus.Resuming => AiExecutionControlDecision.Continue(
                    state.Status,
                    state.Reason),

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

        /// <inheritdoc />
        public async Task<AiExecutionControlState> MarkPausedAsync(
            string executionId,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            var state = await ApplyTransitionAsync(
                    executionId,
                    existing => ApplyMarkPaused(existing, executionId, requestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordControlLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.Control.Paused,
                    AiDecisionLedgerOutcome.Applied,
                    "Execution marked as paused after active work drained.",
                    CreateControlMetadata(state),
                    cancellationToken)
                .ConfigureAwait(false);

            return state;
        }

        /// <inheritdoc />
        public async Task<AiExecutionControlState> MarkRunningAsync(
            string executionId,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ValidateExecutionId(executionId);

            var state = await ApplyTransitionAsync(
                    executionId,
                    existing => ApplyMarkRunning(existing, executionId, requestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            await RecordControlLedgerEventAsync(
                    state,
                    AiDecisionLedgerEvents.Control.Resumed,
                    AiDecisionLedgerOutcome.Applied,
                    "Execution marked as running after resume or human input.",
                    CreateControlMetadata(state),
                    cancellationToken)
                .ConfigureAwait(false);

            return state;
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

        /// <summary>
        /// Applies an effective paused transition after active claimed work has drained.
        /// </summary>
        /// <param name="existing">The existing control state.</param>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="requestedBy">The optional identity confirming the paused state.</param>
        /// <returns>The updated control state.</returns>
        private static AiExecutionControlState ApplyMarkPaused(
            AiExecutionControlState? existing,
            string executionId,
            string? requestedBy)
        {
            var state = CloneOrCreate(existing, executionId);

            if (state.Status is AiExecutionControlStatus.Cancelled or AiExecutionControlStatus.Cancelling)
            {
                return state;
            }

            if (state.Status == AiExecutionControlStatus.Paused)
            {
                return state;
            }

            if (state.Status != AiExecutionControlStatus.Pausing &&
                state.PendingAction != AiExecutionControlAction.Pause)
            {
                return state;
            }

            state.Status = AiExecutionControlStatus.Paused;
            state.PendingAction = AiExecutionControlAction.None;
            state.RequestedBy = requestedBy ?? state.RequestedBy;
            state.PausedAtUtc ??= DateTime.UtcNow;

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

        /// <summary>
        /// Applies an effective running transition after a paused, waiting, or resuming execution is allowed to advance again.
        /// </summary>
        /// <param name="existing">The existing control state.</param>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <param name="requestedBy">The optional identity confirming the running state.</param>
        /// <returns>The updated control state.</returns>
        private static AiExecutionControlState ApplyMarkRunning(
            AiExecutionControlState? existing,
            string executionId,
            string? requestedBy)
        {
            var state = CloneOrCreate(existing, executionId);

            if (state.Status is AiExecutionControlStatus.Cancelled or AiExecutionControlStatus.Cancelling)
            {
                return state;
            }

            if (state.Status == AiExecutionControlStatus.Running &&
                state.PendingAction == AiExecutionControlAction.None)
            {
                return state;
            }

            if (state.Status != AiExecutionControlStatus.Resuming &&
                state.PendingAction != AiExecutionControlAction.Resume &&
                state.PendingAction != AiExecutionControlAction.SubmitInput)
            {
                return state;
            }

            state.Status = AiExecutionControlStatus.Running;
            state.PendingAction = AiExecutionControlAction.None;
            state.RequestedBy = requestedBy ?? state.RequestedBy;

            return state;
        }

        private async Task RecordControlLedgerEventAsync(
            AiExecutionControlState state,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? reason,
            IReadOnlyDictionary<string, string>? metadata,
            CancellationToken cancellationToken)
        {
            await RecordLedgerEventAsync(
                    state,
                    ControlScope,
                    AiDecisionLedgerCategory.Control,
                    eventType,
                    outcome,
                    reason,
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task RecordHumanInputLedgerEventAsync(
            AiExecutionControlState state,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? reason,
            IReadOnlyDictionary<string, string>? metadata,
            CancellationToken cancellationToken)
        {
            var stepName = !string.IsNullOrWhiteSpace(state.WaitingStepName)
                ? state.WaitingStepName!
                : !string.IsNullOrWhiteSpace(state.WaitingKey)
                    ? state.WaitingKey!
                    : HumanInputScope;

            await RecordLedgerEventAsync(
                    state,
                    stepName,
                    AiDecisionLedgerCategory.HumanInput,
                    eventType,
                    outcome,
                    reason,
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task RecordLedgerEventAsync(
            AiExecutionControlState state,
            string stepName,
            AiDecisionLedgerCategory category,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? reason,
            IReadOnlyDictionary<string, string>? metadata,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

            if (_observability?.Ledger is null)
            {
                return;
            }

            var workerId = string.IsNullOrWhiteSpace(state.RequestedBy)
                ? "execution-control"
                : state.RequestedBy!;

            var correlationContext = AiRuntimeCorrelationContextHelper.Create(
                state.ExecutionId,
                ControlPipelineKey,
                stepName,
                workerId,
                claimToken: null,
                concurrencyContext: null);

            await _observability.Ledger
                .RecordAsync(
                    correlationContext,
                    category,
                    eventType,
                    outcome,
                    reason,
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static IReadOnlyDictionary<string, string> CreateControlMetadata(
            AiExecutionControlState state)
        {
            return new Dictionary<string, string>
            {
                ["status"] = state.Status.ToString(),
                ["pending.action"] = state.PendingAction.ToString(),
                ["requested.by"] = state.RequestedBy ?? string.Empty,
                ["reason"] = state.Reason ?? string.Empty,
                ["version"] = state.Version.ToString()
            };
        }

        private static IReadOnlyDictionary<string, string> CreateHumanInputMetadata(
            AiExecutionControlState state)
        {
            return new Dictionary<string, string>
            {
                ["status"] = state.Status.ToString(),
                ["pending.action"] = state.PendingAction.ToString(),
                ["requested.by"] = state.RequestedBy ?? string.Empty,
                ["waiting.key"] = state.WaitingKey ?? string.Empty,
                ["waiting.step.name"] = state.WaitingStepName ?? string.Empty,
                ["input.keys.count"] = state.Input.Count.ToString(),
                ["version"] = state.Version.ToString()
            };
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