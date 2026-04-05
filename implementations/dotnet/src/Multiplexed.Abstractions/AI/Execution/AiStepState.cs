using System.Text.Json.Serialization;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the durable runtime state of a single step within an AI pipeline execution.
    ///
    /// PURPOSE:
    /// - This object is the source of truth for DAG step lifecycle
    /// - It drives scheduling, retry eligibility, completion, and failure semantics
    /// - It is shared across local execution and distributed Redis/Lua coordination
    ///
    /// IMPORTANT:
    /// - This model stores durable execution state only
    /// - Distributed retry behavior must be derived from this object
    /// - Local in-process attempt tracking must not replace or override this state
    ///
    /// DESIGN:
    /// - Each step is uniquely identified by <see cref="StepName"/>
    /// - State transitions should remain deterministic and monotonic
    /// - Computed helper properties are excluded from persistence
    /// </summary>
    public sealed class AiStepState
    {
        // ---------------------------------------------------------------------
        // IDENTITY
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the unique logical name of the step within the pipeline execution.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        // ---------------------------------------------------------------------
        // STATUS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the current lifecycle status of the step.
        ///
        /// This is the primary state used for:
        /// - DAG readiness selection
        /// - retry scheduling
        /// - completion and failure transitions
        /// - distributed claim ownership and recovery
        ///
        /// Typical lifecycle:
        /// None -> Ready -> Running -> Completed
        ///                      -> WaitingForRetry -> Ready -> Running
        ///                      -> Failed
        /// </summary>
        public AiStepExecutionStatus Status { get; set; } = AiStepExecutionStatus.None;

        // ---------------------------------------------------------------------
        // DAG
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the list of prerequisite step names that must be completed
        /// before this step becomes eligible for execution.
        /// </summary>
        public List<string> DependsOn { get; set; } = new();

        // ---------------------------------------------------------------------
        // DISTRIBUTED CLAIM
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the identifier of the worker that currently owns this step claim.
        /// Null when the step is not currently claimed.
        /// </summary>
        public string? ClaimedBy { get; set; }

        /// <summary>
        /// Gets or sets the unique claim token used to validate ownership
        /// during distributed completion and failure operations.
        /// </summary>
        public string? ClaimToken { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp at which the step was claimed.
        /// Used for timeout detection and distributed recovery.
        /// </summary>
        public DateTime? ClaimedAtUtc { get; set; }

        // ---------------------------------------------------------------------
        // TIMING
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the UTC timestamp when execution first started for this step.
        ///
        /// IMPORTANT:
        /// - This value is preserved across retries once set
        /// - It represents the first logical start of the step lifecycle
        /// </summary>
        public DateTime? StartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the last state mutation.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the step reached a terminal state.
        /// Terminal states are typically Completed or Failed.
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the total execution duration once the step reaches a terminal state.
        /// </summary>
        public TimeSpan? Duration { get; set; }

        // ---------------------------------------------------------------------
        // ERROR / RETRY
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the last error message associated with the step.
        ///
        /// This may represent either:
        /// - a retryable business failure
        /// - a terminal failure
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the number of business retry attempts already scheduled
        /// after the initial execution attempt.
        ///
        /// SEMANTICS:
        /// - The initial execution attempt is not counted here
        /// - This counter is incremented only when a failed execution is promoted
        ///   into <see cref="AiStepExecutionStatus.WaitingForRetry"/>
        /// - Infrastructure timeout recovery does not modify this counter
        ///
        /// EXAMPLE:
        /// - RetryCount = 0 -> no retry scheduled yet
        /// - RetryCount = 1 -> one retry has already been scheduled
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the number of infrastructure recovery operations.
        ///
        /// This counter is incremented when a distributed running claim is recovered
        /// after timeout or worker loss.
        ///
        /// IMPORTANT:
        /// - This is intentionally separate from <see cref="RetryCount"/>
        /// - Infrastructure recovery must not consume business retry budget
        /// </summary>
        public int RecoveryCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of business retry attempts allowed
        /// after the initial execution attempt.
        ///
        /// EXAMPLE:
        /// - MaxRetries = 0 -> no retry after the first failure
        /// - MaxRetries = 1 -> one retry after the first failure
        /// - MaxRetries = 2 -> two retries after the first failure
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the UTC timestamp when the next retry becomes eligible.
        ///
        /// While this value is still in the future, the step must not be claimed again.
        /// </summary>
        public DateTime? NextRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the retry delay in milliseconds used when scheduling the next retry attempt.
        /// </summary>
        public int RetryDelayMs { get; set; }

        /// <summary>
        /// Gets or sets the retry delay as a <see cref="TimeSpan"/>.
        /// This is a convenience wrapper around <see cref="RetryDelayMs"/>.
        /// </summary>
        [JsonIgnore]
        public TimeSpan RetryDelay
        {
            get => TimeSpan.FromMilliseconds(RetryDelayMs);
            set => RetryDelayMs = (int)Math.Max(0, value.TotalMilliseconds);
        }

        /// <summary>
        /// Gets or sets the distributed claim timeout in seconds.
        ///
        /// When exceeded, a running step may be recovered and requeued.
        /// </summary>
        public int? ClaimTimeoutSeconds { get; set; }

        // ---------------------------------------------------------------------
        // INPUT / CONFIG
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets the resolved runtime input values for this step instance.
        /// These values are prepared during pipeline binding and resolution.
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
        /// This is populated when the step completes successfully.
        /// </summary>
        public AiStepResult? Result { get; set; }

        // ---------------------------------------------------------------------
        // VERSIONING
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the state version.
        /// Used for optimistic concurrency and Lua-based CAS semantics.
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
        /// retry counters. It is used both for initial readiness and requeue scenarios.
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
        /// Marks the step as running after a successful distributed claim.
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
        /// This is a non-terminal retry state.
        /// While in this state, the step must not be claimed until <see cref="NextRetryAtUtc"/>
        /// is reached or passed.
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
        /// - This is an infrastructure recovery transition
        /// - This is NOT a business retry decision
        /// - <see cref="RecoveryCount"/> is incremented
        /// - <see cref="RetryCount"/> is not modified
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
        /// Applies retry-or-fail semantics for a failed step execution attempt.
        ///
        /// SEMANTICS:
        /// - <see cref="RetryCount"/> tracks scheduled business retries only
        /// - The initial execution attempt is not counted in <see cref="RetryCount"/>
        /// - If <see cref="RetryCount"/> is still lower than <see cref="MaxRetries"/>,
        ///   the step transitions to <see cref="AiStepExecutionStatus.WaitingForRetry"/>
        /// - Otherwise the step becomes terminally <see cref="AiStepExecutionStatus.Failed"/>
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
        /// Gets a value indicating whether the step has completed successfully.
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
        /// A step is terminal when it is either Completed or Failed.
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
        /// - This helper does not validate dependency satisfaction
        /// - The selector must still validate DAG prerequisites separately
        ///
        /// This is a computed property and is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public bool IsSchedulable =>
            Status == AiStepExecutionStatus.Ready ||
            Status == AiStepExecutionStatus.None;
    }
}