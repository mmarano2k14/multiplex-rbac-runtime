using Multiplexed.Abstractions.AI.Metrics.Policy;
using Multiplexed.AI.Abstractions.AI.Policies;
using System;
using System.Threading;

namespace Multiplexed.AI.Runtime.Metrics.Policy
{
    /// <summary>
    /// In-memory implementation of policy execution metrics.
    ///
    /// PURPOSE:
    /// - Track policy execution frequency, success, and failures.
    /// - Provide visibility into policy behavior and performance.
    ///
    /// THREAD SAFETY:
    /// - This implementation is safe for singleton usage.
    /// - Uses atomic operations for all counters.
    ///
    /// IMPORTANT:
    /// - This class only records metrics.
    /// - It must not influence policy decisions or execution flow.
    /// </summary>
    public sealed class AiPolicyMetrics : IAiPolicyMetrics
    {
        private long _policyExecutionCount;
        private long _policySuccessCount;
        private long _policyFailureCount;

        // 🔥 NEW (decision level)
        private long _policyRetryCount;
        private long _policyBlockCount;

        private long _lastExecutionDurationMs;
        private long _totalExecutionDurationMs;

        private string? _lastPolicyName;
        private long _lastExecutionTimestamp;

        /// <inheritdoc />
        public void RecordExecution(
            string executionId,
            string policyName,
            bool success,
            TimeSpan duration)
        {
            _ = executionId;

            Interlocked.Increment(ref _policyExecutionCount);

            if (success)
            {
                Interlocked.Increment(ref _policySuccessCount);
            }
            else
            {
                Interlocked.Increment(ref _policyFailureCount);
            }

            var durationMs = (long)Math.Max(0, duration.TotalMilliseconds);

            Interlocked.Exchange(ref _lastExecutionDurationMs, durationMs);
            Interlocked.Add(ref _totalExecutionDurationMs, durationMs);

            _lastPolicyName = policyName;
            Interlocked.Exchange(ref _lastExecutionTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <inheritdoc />
        public void RecordFailure(string executionId, string policyName)
        {
            _ = executionId;

            Interlocked.Increment(ref _policyFailureCount);

            _lastPolicyName = policyName;
            Interlocked.Exchange(ref _lastExecutionTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <inheritdoc />
        public void RecordDecision(
            string executionId,
            string policyName,
            AiPolicyResultKind kind)
        {
            _ = executionId;

            switch (kind)
            {
                case AiPolicyResultKind.Retry:
                    Interlocked.Increment(ref _policyRetryCount);
                    break;

                case AiPolicyResultKind.Block:
                    Interlocked.Increment(ref _policyBlockCount);
                    break;

                case AiPolicyResultKind.Success:
                default:
                    // success already tracked in RecordExecution
                    break;
            }

            _lastPolicyName = policyName;
            Interlocked.Exchange(ref _lastExecutionTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>
        /// Gets the total number of policy executions.
        /// </summary>
        public long PolicyExecutionCount => Interlocked.Read(ref _policyExecutionCount);

        /// <summary>
        /// Gets the number of successful policy executions.
        /// </summary>
        public long PolicySuccessCount => Interlocked.Read(ref _policySuccessCount);

        /// <summary>
        /// Gets the number of failed policy executions.
        /// </summary>
        public long PolicyFailureCount => Interlocked.Read(ref _policyFailureCount);

        /// <summary>
        /// Gets the number of retry decisions produced by policies.
        /// </summary>
        public long PolicyRetryCount => Interlocked.Read(ref _policyRetryCount);

        /// <summary>
        /// Gets the number of block decisions produced by policies.
        /// </summary>
        public long PolicyBlockCount => Interlocked.Read(ref _policyBlockCount);

        /// <summary>
        /// Gets the duration (in milliseconds) of the last policy execution.
        /// </summary>
        public long LastExecutionDurationMs => Interlocked.Read(ref _lastExecutionDurationMs);

        /// <summary>
        /// Gets the total accumulated execution duration (in milliseconds).
        /// </summary>
        public long TotalExecutionDurationMs => Interlocked.Read(ref _totalExecutionDurationMs);

        /// <summary>
        /// Gets the name of the last executed policy.
        /// </summary>
        public string? LastPolicyName => _lastPolicyName;

        /// <summary>
        /// Gets the timestamp (Unix ms) of the last execution.
        /// </summary>
        public long LastExecutionTimestamp => Interlocked.Read(ref _lastExecutionTimestamp);
    }
}