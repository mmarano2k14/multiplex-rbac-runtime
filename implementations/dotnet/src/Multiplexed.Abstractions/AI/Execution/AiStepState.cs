using System.Text.Json.Serialization;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the mutable runtime state of a single step instance
    /// within an AI pipeline execution.
    ///
    /// DESIGN NOTES:
    /// - Step state is the source of truth for DAG orchestration
    /// - Each step instance is tracked independently by StepName
    /// - This state is used by both local scheduling and distributed Redis/Lua coordination
    ///
    /// IMPORTANT:
    /// This object intentionally stores only durable runtime state.
    /// Computed helper properties are marked with <see cref="JsonIgnoreAttribute"/>
    /// so they do not pollute persisted JSON payloads.
    /// </summary>
    public sealed class AiStepState
    {
        // ---------------------------------------------------------------------
        // IDENTITY
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the unique step instance name.
        /// This is the logical identity of the step inside the pipeline execution.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        // ---------------------------------------------------------------------
        // STATUS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the current execution lifecycle status of the step.
        ///
        /// This is the primary driver for:
        /// - DAG readiness selection
        /// - retry eligibility
        /// - completion and failure semantics
        /// - distributed claim recovery
        /// </summary>
        public AiStepExecutionStatus Status { get; set; } = AiStepExecutionStatus.None;

        // ---------------------------------------------------------------------
        // DAG
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the list of prerequisite step names that must complete
        /// before this step becomes eligible for execution.
        /// </summary>
        public List<string> DependsOn { get; set; } = new();

        // ---------------------------------------------------------------------
        // DISTRIBUTED CLAIM
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the worker identifier that currently owns this step claim.
        /// Null when the step is not currently claimed.
        /// </summary>
        public string? ClaimedBy { get; set; }

        /// <summary>
        /// Gets or sets the unique claim token used to validate ownership
        /// for distributed completion and failure operations.
        /// </summary>
        public string? ClaimToken { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp at which the step was claimed.
        /// Used for timeout recovery in distributed execution.
        /// </summary>
        public DateTime? ClaimedAtUtc { get; set; }

        // ---------------------------------------------------------------------
        // TIMING
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the UTC timestamp when execution first started for this step.
        /// This is preserved across retries once set.
        /// </summary>
        public DateTime? StartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the last state mutation.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the step reached a terminal state.
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the total execution duration when the step reaches a terminal state.
        /// </summary>
        public TimeSpan? Duration { get; set; }

        // ---------------------------------------------------------------------
        // ERROR / RETRY
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the last error message associated with the step.
        /// This may represent either:
        /// - a retryable failure
        /// - a terminal failure
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the business retry count.
        ///
        /// This counter is incremented when the step execution itself fails
        /// and retry policy decides to schedule another attempt.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the infrastructure recovery count.
        ///
        /// This counter is incremented when a distributed running claim is recovered
        /// after timeout or worker loss.
        ///
        /// IMPORTANT:
        /// This is intentionally separate from <see cref="RetryCount"/>
        /// so infrastructure recovery does not consume business retry budget.
        /// </summary>
        public int RecoveryCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed number of business retries.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the UTC timestamp when the next retry becomes eligible.
        /// While this value is still in the future, the step must not be claimed again.
        /// </summary>
        public DateTime? NextRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the retry delay applied when scheduling the next retry attempt.
        /// If null, a default fallback delay is used by the runtime.
        /// </summary>
        public int RetryDelayMs { get; set; }

        [JsonIgnore]
        public TimeSpan RetryDelay
        {
            get => TimeSpan.FromMilliseconds(RetryDelayMs);
            set => RetryDelayMs = (int)Math.Max(0, value.TotalMilliseconds);
        }

        /// <summary>
        /// Gets or sets the distributed claim timeout in seconds.
        /// When exceeded, a running step may be recovered and requeued.
        /// </summary>
        public int? ClaimTimeoutSeconds { get; set; }

        // ---------------------------------------------------------------------
        // INPUT / CONFIG
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets the resolved runtime input values for this step instance.
        /// These values are prepared during pipeline binding / resolution.
        /// </summary>
        public Dictionary<string, object?> Inputs { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the resolved static configuration values for this step instance.
        /// </summary>
        public Dictionary<string, object?> Config { get; set; } = new(StringComparer.Ordinal);

        // ---------------------------------------------------------------------
        // RESULT
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the final execution result of the step.
        /// Populated when the step completes successfully.
        /// </summary>
        public AiStepResult? Result { get; set; }

        // ---------------------------------------------------------------------
        // VERSIONING
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the state version.
        /// Used to support optimistic concurrency and Lua-based CAS semantics.
        /// </summary>
        public long Version { get; set; }

        // ---------------------------------------------------------------------
        // SETTERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Replaces the current input dictionary with a new immutable copy.
        /// </summary>
        public void SetInputs(IReadOnlyDictionary<string, object?>? inputs)
        {
            Inputs = inputs is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(inputs, StringComparer.Ordinal);
        }

        /// <summary>
        /// Replaces the current configuration dictionary with a new immutable copy.
        /// </summary>
        public void SetConfig(IReadOnlyDictionary<string, object?>? config)
        {
            Config = config is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(config, StringComparer.Ordinal);
        }

        // ---------------------------------------------------------------------
        // STATE TRANSITIONS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Marks the step as ready for execution.
        ///
        /// This clears any active distributed claim metadata but does not alter
        /// retry counters. It is used both for initial readiness and for requeue scenarios.
        /// </summary>
        public void MarkReady()
        {
            Status = AiStepExecutionStatus.Ready;
            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;
            UpdatedAtUtc = DateTime.UtcNow;
            Version++;
        }

        /// <summary>
        /// Marks the step as running after a successful claim.
        ///
        /// In distributed mode, the supplied claim token is later used to validate
        /// ownership for completion and failure transitions.
        /// </summary>
        public void MarkRunning(string workerId, string claimToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Running;
            ClaimedBy = workerId;
            ClaimToken = claimToken;
            ClaimedAtUtc = now;
            StartedAtUtc ??= now;
            UpdatedAtUtc = now;
            Version++;
        }

        /// <summary>
        /// Marks the step as completed successfully.
        ///
        /// This transition:
        /// - stores the final result
        /// - clears retry timing
        /// - clears claim ownership
        /// - computes final duration when possible
        /// </summary>
        public void MarkCompleted(AiStepResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Completed;
            Result = result;
            Error = null;
            NextRetryAtUtc = null;
            CompletedAtUtc = now;
            UpdatedAtUtc = now;
            Duration = StartedAtUtc.HasValue ? now - StartedAtUtc.Value : null;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Marks the step as terminally failed.
        ///
        /// This should only be used when retry budget has been exhausted
        /// or when the failure is considered non-retriable.
        /// </summary>
        public void MarkFailed(string? error)
        {
            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Failed;
            Error = error;
            NextRetryAtUtc = null;
            CompletedAtUtc = now;
            UpdatedAtUtc = now;
            Duration = StartedAtUtc.HasValue ? now - StartedAtUtc.Value : null;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Marks the step as waiting for a future retry attempt.
        ///
        /// This is a non-terminal transition used by retry-aware execution.
        /// </summary>
        public void MarkWaitingForRetry(string? error, DateTime nextRetryAtUtc)
        {
            Status = AiStepExecutionStatus.WaitingForRetry;
            Error = error;
            NextRetryAtUtc = nextRetryAtUtc;
            UpdatedAtUtc = DateTime.UtcNow;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Requeues a timed-out running step back to Ready.
        ///
        /// IMPORTANT:
        /// This is an infrastructure recovery transition, not a business retry decision.
        /// It increments <see cref="RecoveryCount"/> and does not consume business retry budget.
        /// </summary>
        public void MarkRequeuedAfterTimeout()
        {
            Status = AiStepExecutionStatus.Ready;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;

            UpdatedAtUtc = DateTime.UtcNow;
            RecoveryCount++;
            Version++;
        }

        /// <summary>
        /// Promotes a retry-waiting step back to Ready when its retry window has opened.
        ///
        /// If the step is not in <see cref="AiStepExecutionStatus.WaitingForRetry"/>,
        /// or if the retry time has not yet been reached, this method does nothing.
        /// </summary>
        public void PromoteRetryToReadyIfDue(DateTime utcNow)
        {
            if (Status != AiStepExecutionStatus.WaitingForRetry)
                return;

            if (!NextRetryAtUtc.HasValue)
                return;

            if (NextRetryAtUtc.Value > utcNow)
                return;

            Status = AiStepExecutionStatus.Ready;
            NextRetryAtUtc = null;
            UpdatedAtUtc = utcNow;
            Version++;
        }

        /// <summary>
        /// Applies retry-or-fail semantics for a failed step attempt.
        ///
        /// Behavior:
        /// - If retry budget remains, the step moves to WaitingForRetry
        /// - Otherwise the step becomes terminally Failed
        /// </summary>
        public void MarkRetryOrFail(string? error, DateTime utcNow)
        {
            Error = error;

            if (RetryCount < MaxRetries)
            {
                RetryCount++;

                var nextRetryAtUtc = utcNow.AddMilliseconds(
                    RetryDelayMs > 0 ? RetryDelayMs : 2000);

                MarkWaitingForRetry(error, nextRetryAtUtc);
                return;
            }

            MarkFailed(error);
        }

        // ---------------------------------------------------------------------
        // COMPUTED HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets a value indicating whether the step is completed successfully.
        ///
        /// This is a computed property and is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public bool IsCompleted => Status == AiStepExecutionStatus.Completed;

        /// <summary>
        /// Gets a value indicating whether the step is currently running.
        ///
        /// This is a computed property and is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public bool IsRunning => Status == AiStepExecutionStatus.Running;

        /// <summary>
        /// Gets a value indicating whether the step is terminally failed.
        ///
        /// This is a computed property and is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public bool IsFailed => Status == AiStepExecutionStatus.Failed;

        /// <summary>
        /// Gets a value indicating whether the step is currently waiting for retry eligibility.
        ///
        /// This is a computed property and is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public bool IsWaitingForRetry => Status == AiStepExecutionStatus.WaitingForRetry;

        /// <summary>
        /// Gets a value indicating whether the step is terminal.
        ///
        /// This is a computed property and is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public bool IsTerminal =>
            Status == AiStepExecutionStatus.Completed ||
            Status == AiStepExecutionStatus.Failed;

        /// <summary>
        /// Gets a value indicating whether the step is locally schedulable.
        ///
        /// NOTE:
        /// This helper does not validate dependency satisfaction.
        /// The selector must still validate DAG prerequisites separately.
        ///
        /// This is a computed property and is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public bool IsSchedulable =>
            Status == AiStepExecutionStatus.Ready ||
            Status == AiStepExecutionStatus.None;
    }
}