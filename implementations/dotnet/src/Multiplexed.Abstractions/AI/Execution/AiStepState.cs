using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Retry;
using System.Text.Json.Serialization;

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
    /// PAYLOAD EVOLUTION:
    /// - <see cref="InputPayloads"/> and <see cref="ConfigPayloads"/> are additive only
    /// - Existing <see cref="Inputs"/> and <see cref="Config"/> remain fully supported
    /// - Payload-aware accessors prefer payload-backed values when present
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
        [Obsolete("Use RetryState.RetryCount instead. This property is kept for backward compatibility during Retry Engine migration.")]
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
        [Obsolete("Use Retry.MaxRetries instead. This property is kept for backward compatibility during Retry Engine migration.")]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the UTC timestamp when the next retry becomes eligible.
        ///
        /// While this value is still in the future, the step must not be claimed again.
        /// </summary>
        [Obsolete("Use RetryState.NextRetryAtUtc instead. This property is kept for backward compatibility during Retry Engine migration.")]
        public DateTime? NextRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the retry delay in milliseconds used when scheduling the next retry attempt.
        /// </summary>
        [Obsolete("Use Retry.BaseDelayMs or RetryState decision metadata instead. This property is kept for backward compatibility during Retry Engine migration.")]
        public int RetryDelayMs { get; set; }

        /// <summary>
        /// Gets or sets the retry delay as a <see cref="TimeSpan"/>.
        /// This is a convenience wrapper around <see cref="RetryDelayMs"/>.
        /// </summary>
        [Obsolete("Use Retry.BaseDelayMs or IAiRetryScheduler instead. This property is kept for backward compatibility during Retry Engine migration.")]
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
        /// Gets or sets the optional payload-backed representation of resolved runtime inputs.
        ///
        /// PURPOSE:
        /// - Enables large input values to be represented by compact payload references
        /// - Supports future ledger compaction without removing inline <see cref="Inputs"/>
        ///
        /// COMPATIBILITY:
        /// - Existing callers may continue using <see cref="Inputs"/>
        /// - Payload-aware callers should use <see cref="GetInputAsync"/> or <see cref="GetInput"/>
        ///
        /// PRECEDENCE:
        /// - Payload-backed input values take priority in payload-aware accessors
        /// </summary>
        public Dictionary<string, AiStoredPayload>? InputPayloads { get; set; }

        /// <summary>
        /// Gets the resolved static configuration values for this step instance.
        /// </summary>
        public Dictionary<string, object?> Config { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the optional payload-backed representation of resolved static configuration values.
        ///
        /// PURPOSE:
        /// - Enables large configuration values to be represented by compact payload references
        /// - Supports future ledger compaction without removing inline <see cref="Config"/>
        ///
        /// COMPATIBILITY:
        /// - Existing callers may continue using <see cref="Config"/>
        /// - Payload-aware callers should use <see cref="GetConfigAsync"/> or <see cref="GetConfig"/>
        ///
        /// PRECEDENCE:
        /// - Payload-backed configuration values take priority in payload-aware accessors
        /// </summary>
        public Dictionary<string, AiStoredPayload>? ConfigPayloads { get; set; }

        /// <summary>
        /// Gets or sets the retry policy definition resolved for the step.
        /// </summary>
        /// <remarks>
        /// This property represents the declarative retry configuration associated with the step,
        /// typically resolved from config.retry during pipeline initialization.
        /// It defines retry policies, retry limits, and delay strategies, but does not contain
        /// mutable execution state.
        /// </remarks>
        public AiRetryPolicyDefinition? Retry { get; set; }

        /// <summary>
        /// Gets or sets the runtime retry state associated with the step.
        /// </summary>
        /// <remarks>
        /// This property contains mutable retry execution data, including retry counters,
        /// scheduling timestamps, and the last retry decision applied by the retry engine.
        /// This state is separate from Retry to distinguish configuration from execution state.
        /// </remarks>
        public AiStepRetryState? RetryState { get; set; }

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
        // PAYLOAD-AWARE INPUT / CONFIG ACCESS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Retrieves an input value using payload resolution when available.
        ///
        /// BEHAVIOR:
        /// - If a payload exists for the requested key, it is resolved through the provided resolver
        /// - Otherwise, the method falls back to the inline <see cref="Inputs"/> dictionary
        ///
        /// IMPORTANT:
        /// - This method does not mutate state
        /// - Payload-backed inputs take priority over inline inputs
        /// - Existing callers using <see cref="Inputs"/> remain fully supported
        /// </summary>
        public async Task<object?> GetInputAsync(
            string key,
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (InputPayloads != null &&
                InputPayloads.TryGetValue(key, out var payload))
            {
                return await resolver.ResolveAsync(payload, cancellationToken);
            }

            Inputs.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>
        /// Synchronous compatibility helper for retrieving an input value using payload
        /// resolution when available.
        ///
        /// Prefer <see cref="GetInputAsync"/> in async runtime paths.
        /// </summary>
        public object? GetInput(
            string key,
            IAiExecutionPayloadResolver resolver)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (InputPayloads != null &&
                InputPayloads.TryGetValue(key, out var payload))
            {
                return resolver.ResolveAsync(payload)
                    .GetAwaiter()
                    .GetResult();
            }

            Inputs.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>
        /// Retrieves a configuration value using payload resolution when available.
        ///
        /// BEHAVIOR:
        /// - If a payload exists for the requested key, it is resolved through the provided resolver
        /// - Otherwise, the method falls back to the inline <see cref="Config"/> dictionary
        ///
        /// IMPORTANT:
        /// - This method does not mutate state
        /// - Payload-backed configuration values take priority over inline configuration values
        /// - Existing callers using <see cref="Config"/> remain fully supported
        /// </summary>
        public async Task<object?> GetConfigAsync(
            string key,
            IAiExecutionPayloadResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (ConfigPayloads != null &&
                ConfigPayloads.TryGetValue(key, out var payload))
            {
                return await resolver.ResolveAsync(payload, cancellationToken);
            }

            Config.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>
        /// Synchronous compatibility helper for retrieving a configuration value using
        /// payload resolution when available.
        ///
        /// Prefer <see cref="GetConfigAsync"/> in async runtime paths.
        /// </summary>
        public object? GetConfig(
            string key,
            IAiExecutionPayloadResolver resolver)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(resolver);

            if (ConfigPayloads != null &&
                ConfigPayloads.TryGetValue(key, out var payload))
            {
                return resolver.ResolveAsync(payload)
                    .GetAwaiter()
                    .GetResult();
            }

            Config.TryGetValue(key, out var value);
            return value;
        }

        // ---------------------------------------------------------------------
        // STATE TRANSITIONS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Marks the step as ready for execution.
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
        /// </summary>
        [Obsolete("Use Retry Engine decision flow instead. This method will be removed after Redis/Lua retry integration.")]
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

        [JsonIgnore]
        public bool IsCompleted => Status == AiStepExecutionStatus.Completed;

        [JsonIgnore]
        public bool IsRunning => Status == AiStepExecutionStatus.Running;

        [JsonIgnore]
        public bool IsFailed => Status == AiStepExecutionStatus.Failed;

        [JsonIgnore]
        public bool IsWaitingForRetry => Status == AiStepExecutionStatus.WaitingForRetry;

        [JsonIgnore]
        public bool IsTerminal =>
            Status == AiStepExecutionStatus.Completed ||
            Status == AiStepExecutionStatus.Failed;

        [JsonIgnore]
        public bool IsSchedulable =>
            Status == AiStepExecutionStatus.Ready ||
            Status == AiStepExecutionStatus.None;

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

        public bool IsLeaseExpired(DateTime utcNow)
        {
            if (Status != AiStepExecutionStatus.Running)
                return false;

            if (!LeaseExpiresAtUtc.HasValue)
                return false;

            return LeaseExpiresAtUtc.Value <= utcNow;
        }

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