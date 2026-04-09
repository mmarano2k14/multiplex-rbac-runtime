using System.Text.Json.Serialization;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the durable runtime state of a single step within an AI pipeline execution.
    ///
    /// PURPOSE:
    /// - This object is the source of truth for DAG step lifecycle
    /// - It drives scheduling, retry eligibility, completion, failure, and recovery semantics
    /// - It is shared across local execution and distributed Redis/Lua coordination
    ///
    /// IMPORTANT:
    /// - This model stores durable execution state only
    /// - Distributed retry and recovery behavior must be derived from this object
    /// - Local in-process attempt tracking must not replace or override this state
    ///
    /// DESIGN:
    /// - Each step is uniquely identified by <see cref="StepName"/>
    /// - State transitions remain deterministic and monotonic
    /// - Computed helper properties are excluded from persistence where appropriate
    /// - Distributed ownership is modeled through explicit claim metadata and lease expiration
    ///
    /// INVARIANT MODEL:
    /// - A step in <see cref="AiStepExecutionStatus.Running"/> must represent a claimed in-flight execution
    /// - Terminal states (<see cref="AiStepExecutionStatus.Completed"/> / <see cref="AiStepExecutionStatus.Failed"/>) must not retain claim metadata
    /// - Business retry and infrastructure recovery are intentionally separate concepts
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
        ///
        /// IMPORTANT:
        /// - A new token should be generated for every claim or reclaim
        /// - Completion or failure writes should only succeed when the expected token matches
        /// - This prevents stale workers from persisting results after ownership has changed
        /// </summary>
        public string? ClaimToken { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp at which the step was claimed.
        /// Used for ownership tracing, timeout analysis, and recovery diagnostics.
        /// </summary>
        public DateTime? ClaimedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the current claim lease expires.
        ///
        /// IMPORTANT:
        /// - This value is computed at claim time
        /// - It is the authoritative source for reclaim eligibility
        /// - Distributed workers must use this value instead of recomputing expiration
        ///   from <see cref="ClaimedAtUtc"/> and <see cref="ClaimTimeoutSeconds"/>
        /// </summary>
        public DateTime? LeaseExpiresAtUtc { get; set; }

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
        /// Gets or sets the final serialized execution duration once the step reaches a terminal state.
        ///
        /// SEMANTICS:
        /// - Null while the step has not yet reached a terminal state
        /// - Set when the step transitions to Completed or Failed
        /// - Represents total logical step lifetime from first <see cref="StartedAtUtc"/>
        ///   to terminal <see cref="CompletedAtUtc"/>
        /// - This value is intended for persistence, replay, audit, and snapshot serialization
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
        /// This value is a lease configuration input.
        /// It is used when creating a claim to compute <see cref="LeaseExpiresAtUtc"/>.
        ///
        /// IMPORTANT:
        /// - Recovery should not recompute expiration from this property
        /// - The authoritative recovery boundary is <see cref="LeaseExpiresAtUtc"/>
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
        /// SAFE TRANSITION FROM:
        /// - None
        /// - WaitingForRetry (when retry window is open)
        /// - Running (only through timeout recovery or controlled reset)
        ///
        /// GUARANTEES:
        /// - Clears all distributed claim metadata
        /// - Does not modify retry counters
        /// - Clears terminal timestamps and persisted final duration
        /// </summary>
        public void MarkReady()
        {
            Status = AiStepExecutionStatus.Ready;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;
            LeaseExpiresAtUtc = null;

            CompletedAtUtc = null;
            Duration = null;

            UpdatedAtUtc = DateTime.UtcNow;
            Version++;
        }

        /// <summary>
        /// Marks the step as running after a successful distributed claim.
        ///
        /// INVARIANTS:
        /// - Step must not already be Running
        /// - Step must not already be terminal (Completed / Failed)
        /// - Claim must originate from a schedulable state
        ///
        /// LEASE SEMANTICS:
        /// - Claim ownership starts immediately
        /// - Lease expiration is computed once and persisted on the step state
        /// - Recovery logic must use <see cref="LeaseExpiresAtUtc"/> as the authoritative boundary
        ///
        /// VIOLATION OF THESE RULES INDICATES A DISTRIBUTION OR STATE MACHINE BUG.
        /// </summary>
        public void MarkRunning(string workerId, string claimToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

            if (Status == AiStepExecutionStatus.Running)
                throw new InvalidOperationException($"Step '{StepName}' is already running.");

            if (Status == AiStepExecutionStatus.Completed || Status == AiStepExecutionStatus.Failed)
                throw new InvalidOperationException($"Step '{StepName}' is terminal and cannot be claimed.");

            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Running;
            ClaimedBy = workerId;
            ClaimToken = claimToken;
            ClaimedAtUtc = now;
            LeaseExpiresAtUtc = ClaimTimeoutSeconds.HasValue && ClaimTimeoutSeconds.Value > 0
                ? now.AddSeconds(ClaimTimeoutSeconds.Value)
                : null;
            StartedAtUtc ??= now;
            UpdatedAtUtc = now;
            Version++;
        }

        /// <summary>
        /// Marks the step as completed successfully.
        ///
        /// INVARIANTS:
        /// - The step must currently be Running
        /// - Completion must come from the worker owning the active claim
        ///   (ownership is validated by the store in distributed mode)
        ///
        /// NOTE:
        /// - This is a STEP-level terminal transition
        /// - GLOBAL execution completion is handled separately via convergence + finalization
        ///
        /// GUARANTEES:
        /// - Stores the final result
        /// - Clears retry timing
        /// - Clears claim ownership
        /// - Computes and persists final duration when possible
        /// </summary>
        public void MarkCompleted(AiStepResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (Status != AiStepExecutionStatus.Running)
            {
                throw new InvalidOperationException(
                    $"Step '{StepName}' cannot complete from status '{Status}'.");
            }

            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Completed;
            Result = result;
            Error = null;
            NextRetryAtUtc = null;
            CompletedAtUtc = now;
            UpdatedAtUtc = now;
            Duration = StartedAtUtc.HasValue && now >= StartedAtUtc.Value
                ? now - StartedAtUtc.Value
                : TimeSpan.Zero;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;
            LeaseExpiresAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Marks the step as terminally failed.
        ///
        /// INVARIANTS:
        /// - The step must currently be Running or WaitingForRetry
        /// - This should only be used when retry budget is exhausted
        ///   or the failure is considered non-retriable
        ///
        /// GUARANTEES:
        /// - Persists the terminal timestamp
        /// - Persists the final duration
        /// - Clears claim ownership
        /// </summary>
        public void MarkFailed(string? error)
        {
            if (Status != AiStepExecutionStatus.Running &&
                Status != AiStepExecutionStatus.WaitingForRetry)
            {
                throw new InvalidOperationException(
                    $"Step '{StepName}' cannot fail from status '{Status}'.");
            }

            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Failed;
            Error = error;
            NextRetryAtUtc = null;
            CompletedAtUtc = now;
            UpdatedAtUtc = now;
            Duration = StartedAtUtc.HasValue && now >= StartedAtUtc.Value
                ? now - StartedAtUtc.Value
                : TimeSpan.Zero;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;
            LeaseExpiresAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Marks the step as waiting for a future retry attempt.
        ///
        /// INVARIANTS:
        /// - The step must currently be Running
        /// - RetryCount must not exceed MaxRetries
        ///
        /// IMPORTANT:
        /// - This is a non-terminal retry state
        /// - This method does not increment RetryCount
        /// - While in this state, the step must not be claimed until <see cref="NextRetryAtUtc"/>
        ///   is reached or passed
        /// </summary>
        public void MarkWaitingForRetry(string? error, DateTime nextRetryAtUtc)
        {
            if (Status != AiStepExecutionStatus.Running)
            {
                throw new InvalidOperationException(
                    $"Step '{StepName}' cannot enter WaitingForRetry from status '{Status}'.");
            }

            if (RetryCount > MaxRetries)
            {
                throw new InvalidOperationException(
                    $"RetryCount '{RetryCount}' exceeds MaxRetries '{MaxRetries}' for step '{StepName}'.");
            }

            Status = AiStepExecutionStatus.WaitingForRetry;
            Error = error;
            NextRetryAtUtc = nextRetryAtUtc;
            UpdatedAtUtc = DateTime.UtcNow;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;
            LeaseExpiresAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Requeues a timed-out running step back to Ready.
        ///
        /// INVARIANTS:
        /// - The step must currently be Running
        ///
        /// IMPORTANT:
        /// - This is an infrastructure recovery transition
        /// - This is NOT a business retry decision
        /// - <see cref="RecoveryCount"/> is incremented
        /// - <see cref="RetryCount"/> is not modified
        /// - The current claim lease is fully cleared
        /// </summary>
        public void MarkRequeuedAfterTimeout()
        {
            if (Status != AiStepExecutionStatus.Running)
            {
                throw new InvalidOperationException(
                    $"Step '{StepName}' cannot be recovered from status '{Status}'.");
            }

            Status = AiStepExecutionStatus.Ready;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;
            LeaseExpiresAtUtc = null;

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
        /// INVARIANTS:
        /// - The step must currently be Running
        /// - RetryCount must never exceed MaxRetries
        ///
        /// SEMANTICS:
        /// - <see cref="RetryCount"/> tracks scheduled business retries only
        /// - The initial execution attempt is not counted in <see cref="RetryCount"/>
        /// - If <see cref="RetryCount"/> is still lower than <see cref="MaxRetries"/>,
        ///   the step transitions to <see cref="AiStepExecutionStatus.WaitingForRetry"/>
        /// - Otherwise the step becomes terminally <see cref="AiStepExecutionStatus.Failed"/>
        ///
        /// GUARANTEES:
        /// - RetryCount increments only on business retry
        /// - RecoveryCount is never modified here
        /// </summary>
        public void MarkRetryOrFail(string? error, DateTime utcNow)
        {
            if (Status != AiStepExecutionStatus.Running)
            {
                throw new InvalidOperationException(
                    $"RetryOrFail is invalid for step '{StepName}' from status '{Status}'.");
            }

            if (RetryCount > MaxRetries)
            {
                throw new InvalidOperationException(
                    $"Invalid retry state for step '{StepName}': RetryCount '{RetryCount}' > MaxRetries '{MaxRetries}'.");
            }

            Error = error;

            if (RetryCount < MaxRetries)
            {
                RetryCount++;

                if (RetryCount > MaxRetries)
                {
                    throw new InvalidOperationException(
                        $"Retry overflow detected for step '{StepName}'.");
                }

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

        /// <summary>
        /// Gets the live elapsed duration of the step.
        ///
        /// SEMANTICS:
        /// - Null if the step has never started
        /// - For running steps, returns the live elapsed time from <see cref="StartedAtUtc"/> to now
        /// - For terminal steps, returns the persisted <see cref="Duration"/> when available
        ///
        /// This helper is intended for logs, diagnostics, metrics, and live observability.
        /// It is intentionally excluded from persisted JSON.
        /// </summary>
        [JsonIgnore]
        public TimeSpan? ElapsedDuration
        {
            get
            {
                if (Duration.HasValue)
                    return Duration;

                if (!StartedAtUtc.HasValue)
                    return null;

                var now = DateTime.UtcNow;
                return now >= StartedAtUtc.Value
                    ? now - StartedAtUtc.Value
                    : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Determines whether the current running lease is expired at the supplied UTC time.
        ///
        /// IMPORTANT:
        /// - This helper only returns true for running steps
        /// - A missing lease expiration is treated as non-expired
        /// - Recovery logic can use this helper to decide whether reclaim is legal
        /// </summary>
        public bool IsLeaseExpired(DateTime utcNow)
        {
            if (Status != AiStepExecutionStatus.Running)
                return false;

            if (!LeaseExpiresAtUtc.HasValue)
                return false;

            return LeaseExpiresAtUtc.Value <= utcNow;
        }

        /// <summary>
        /// Determines whether the step currently has an active and valid running lease.
        ///
        /// IMPORTANT:
        /// - This helper only returns true for running steps
        /// - A missing lease expiration is treated as not valid
        /// - Convergence and recovery logic can use this helper to distinguish
        ///   between active work and reclaimable stale work
        /// </summary>
        public bool HasValidLease(DateTime utcNow)
        {
            if (Status != AiStepExecutionStatus.Running)
                return false;

            if (!LeaseExpiresAtUtc.HasValue)
                return false;

            return LeaseExpiresAtUtc.Value > utcNow;
        }
    }
}