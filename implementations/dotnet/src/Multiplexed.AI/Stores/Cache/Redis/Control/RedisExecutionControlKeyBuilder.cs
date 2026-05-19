using System;

namespace Multiplexed.AI.Stores.Cache.Redis.Control
{
    /// <summary>
    /// Builds Redis keys used by the AI execution control store.
    /// </summary>
    /// <remarks>
    /// Execution control keys are intentionally separated from DAG execution state keys.
    /// DAG state stores deterministic execution progress, while control keys store
    /// operator, system, or user-level control state such as pause, resume, cancellation,
    /// and waiting-for-input information.
    /// </remarks>
    public sealed class RedisExecutionControlKeyBuilder
    {
        private const string ExecutionControlPrefix = "ai:execution:control";

        /// <summary>
        /// Builds the Redis key for the durable control state of an execution.
        /// </summary>
        /// <param name="executionId">The durable execution identifier.</param>
        /// <returns>The Redis key used to store execution control state.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="executionId"/> is null, empty, or whitespace.
        /// </exception>
        public string BuildExecutionControlKey(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null, empty, or whitespace.", nameof(executionId));
            }

            return $"{ExecutionControlPrefix}:{executionId}";
        }
    }
}