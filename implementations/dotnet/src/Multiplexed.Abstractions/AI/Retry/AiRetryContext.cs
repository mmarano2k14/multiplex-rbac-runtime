using System;
using System.Collections.Generic;

namespace Multiplexed.AI.Abstractions.AI.Retry
{
    /// <summary>
    /// Provides the runtime context required to evaluate retry behavior for a failed AI step.
    /// </summary>
    /// <remarks>
    /// The retry context is created by the execution engine when a step fails.
    /// It contains execution identity, step identity, retry counters, failure details,
    /// resolved retry configuration, and the step input/config data available at failure time.
    /// </remarks>
    public sealed class AiRetryContext
    {
        /// <summary>
        /// Gets the unique execution identifier.
        /// </summary>
        public required string ExecutionId { get; init; }

        /// <summary>
        /// Gets the unique step identifier within the execution.
        /// </summary>
        public required string StepId { get; init; }

        /// <summary>
        /// Gets the step key or step type associated with the failed step.
        /// </summary>
        public required string StepKey { get; init; }

        /// <summary>
        /// Gets the current retry attempt count already consumed by the step.
        /// </summary>
        public required int RetryCount { get; init; }

        /// <summary>
        /// Gets the maximum number of retry attempts allowed for the step.
        /// </summary>
        public required int MaxRetries { get; init; }

        /// <summary>
        /// Gets the exception that caused the step failure, when available.
        /// </summary>
        public required Exception? Exception { get; init; }

        /// <summary>
        /// Gets the failure reason captured by the runtime or step executor.
        /// </summary>
        public required string? FailureReason { get; init; }

        /// <summary>
        /// Gets the UTC timestamp at which the failure was observed.
        /// </summary>
        public required DateTimeOffset FailedAtUtc { get; init; }

        /// <summary>
        /// Gets the resolved retry policy definition associated with the failed step.
        /// </summary>
        public required AiRetryPolicyDefinition? Retry { get; init; }

        /// <summary>
        /// Gets the step inputs available at the time of failure.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Inputs { get; init; } =
            new Dictionary<string, object?>();

        /// <summary>
        /// Gets the step configuration available at the time of failure.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Config { get; init; } =
            new Dictionary<string, object?>();
    }
}