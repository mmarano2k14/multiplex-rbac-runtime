using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the runtime state of a single step instance within an AI pipeline execution.
    ///
    /// DESIGN PRINCIPLES:
    /// - Each step instance has its own isolated state
    /// - State is keyed by step instance name (not step type)
    /// - This enables multiple instances of the same step type to coexist safely
    /// - This is the foundation for DAG execution
    ///
    /// IMPORTANT:
    /// This class is the source of truth for:
    /// - step lifecycle (Status)
    /// - execution timing
    /// - retry tracking
    /// - inputs and configuration
    /// - execution results
    ///
    /// In DAG mode, the scheduler MUST rely on this state
    /// instead of any global "current step" concept.
    /// </summary>
    public sealed class AiStepState
    {
        // ---------------------------------------------------------------------
        // IDENTITY
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the unique step instance name.
        /// This is the identity of the step within the pipeline execution.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        // ---------------------------------------------------------------------
        // STATUS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the current execution status of the step.
        ///
        /// This property is the primary driver for DAG scheduling decisions.
        /// </summary>
        public AiStepExecutionStatus Status { get; set; } = AiStepExecutionStatus.None;

        // ---------------------------------------------------------------------
        // DAG
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the list of step names that must be completed
        /// before this step becomes eligible for execution.
        /// </summary>
        public List<string> DependsOn { get; set; } = new();

        // ---------------------------------------------------------------------
        // DISTRIBUTED CLAIM
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the worker that currently owns the step claim.
        /// </summary>
        public string? ClaimedBy { get; set; }

        /// <summary>
        /// Gets or sets the unique claim token used to validate completion/failure ownership.
        /// </summary>
        public string? ClaimToken { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the step was claimed.
        /// </summary>
        public DateTime? ClaimedAtUtc { get; set; }

        // ---------------------------------------------------------------------
        // TIMING
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the UTC timestamp when the step execution started.
        /// </summary>
        public DateTime? StartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the step execution was last updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the step execution completed.
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the execution duration.
        /// </summary>
        public TimeSpan? Duration { get; set; }

        // ---------------------------------------------------------------------
        // ERROR / RETRY
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the error message if the step failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the number of execution attempts for this step.
        /// Used for retry policies and observability.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed retries.
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Gets or sets the next retry time (for delayed retry strategies).
        /// </summary>
        public DateTime? NextRetryAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the execution timeout in seconds.
        /// A running step whose claim is older than this timeout may be recovered.
        /// </summary>
        public int? ClaimTimeoutSeconds { get; set; }

        // ---------------------------------------------------------------------
        // INPUT / CONFIG
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets the resolved input values for this step instance.
        ///
        /// These values are populated during pipeline preparation or binding resolution.
        /// </summary>
        public Dictionary<string, object?> Inputs { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the resolved configuration values for this step instance.
        ///
        /// These values are static for the lifetime of the step execution.
        /// </summary>
        public Dictionary<string, object?> Config { get; set; } = new(StringComparer.Ordinal);

        // ---------------------------------------------------------------------
        // RESULT
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the execution result of the step.
        ///
        /// This is the source of truth for:
        /// - output value
        /// - structured data
        /// - downstream step bindings
        /// </summary>
        public AiStepResult? Result { get; set; }

        // ---------------------------------------------------------------------
        // VERSIONING
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the version of the step state.
        ///
        /// This supports optimistic concurrency / Lua-based CAS semantics.
        /// </summary>
        public long Version { get; set; }

        // ---------------------------------------------------------------------
        // SETTERS
        // ---------------------------------------------------------------------

        public void SetInputs(IReadOnlyDictionary<string, object?>? inputs)
        {
            Inputs = inputs is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(inputs, StringComparer.Ordinal);
        }

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
        /// Marks the step as running after a successful distributed claim.
        /// </summary>
        public void MarkRunning(string workerId, string claimToken)
        {
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
        /// Marks the step as successfully completed.
        /// </summary>
        public void MarkCompleted(AiStepResult result)
        {
            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Completed;
            Result = result;
            Error = null;
            CompletedAtUtc = now;
            UpdatedAtUtc = now;
            Duration = StartedAtUtc.HasValue ? now - StartedAtUtc.Value : null;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Marks the step as failed.
        /// </summary>
        public void MarkFailed(string? error)
        {
            var now = DateTime.UtcNow;

            Status = AiStepExecutionStatus.Failed;
            Error = error;
            CompletedAtUtc = now;
            UpdatedAtUtc = now;
            Duration = StartedAtUtc.HasValue ? now - StartedAtUtc.Value : null;

            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;

            Version++;
        }

        /// <summary>
        /// Requeues a timed-out running step.
        /// </summary>
        public void MarkRequeuedAfterTimeout()
        {
            Status = AiStepExecutionStatus.Pending;
            ClaimedBy = null;
            ClaimToken = null;
            ClaimedAtUtc = null;
            UpdatedAtUtc = DateTime.UtcNow;
            RetryCount++;
            Version++;
        }

        // ---------------------------------------------------------------------
        // STATE HELPERS
        // ---------------------------------------------------------------------

        public bool IsCompleted => Status == AiStepExecutionStatus.Completed;

        public bool IsRunning => Status == AiStepExecutionStatus.Running;

        public bool IsFailed => Status == AiStepExecutionStatus.Failed;

        public bool IsTerminal =>
            Status == AiStepExecutionStatus.Completed ||
            Status == AiStepExecutionStatus.Failed;

        /// <summary>
        /// Gets a value indicating whether the step is ready to be scheduled.
        ///
        /// NOTE:
        /// This does NOT check dependencies.
        /// The scheduler must validate dependency satisfaction separately.
        /// </summary>
        public bool IsSchedulable =>
            Status == AiStepExecutionStatus.Pending ||
            Status == AiStepExecutionStatus.None;
    }
}