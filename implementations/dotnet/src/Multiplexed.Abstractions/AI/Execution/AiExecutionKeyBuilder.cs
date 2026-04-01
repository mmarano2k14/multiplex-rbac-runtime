using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Builds stable Redis keys for AI execution persistence and distributed DAG coordination.
    ///
    /// This class centralizes Redis key naming conventions.
    /// It must remain deterministic and stable.
    /// </summary>
    public sealed class AiExecutionKeyBuilder : IAiExecutionKeyBuilder
    {
        private const string Prefix = "ai:execution";

        /// <inheritdoc />
        public string GetExecutionRecordKey(string executionId)
        {
            ValidateExecutionId(executionId);
            return $"{Prefix}:record:{executionId}";
        }

        /// <inheritdoc />
        public string GetExecutionStateKey(string executionId)
        {
            ValidateExecutionId(executionId);
            return $"{Prefix}:state:{executionId}";
        }

        /// <inheritdoc />
        public string GetDagStepIdsKey(string executionId)
        {
            ValidateExecutionId(executionId);
            return $"{Prefix}:steps:{executionId}";
        }

        public string GetDagStepKeyPrefix(string executionId)
        {
            ValidateExecutionId(executionId);
            return $"{Prefix}:step:{executionId}:";
        }

        /// <inheritdoc />
        public string GetDagStepKey(string executionId, string stepId)
        {
            ValidateExecutionId(executionId);
            ValidateStepId(stepId);

            return $"{Prefix}:step:{executionId}:{stepId}";
        }

        /// <inheritdoc />
        public string GetDagClaimKey(string executionId, string stepId)
        {
            ValidateExecutionId(executionId);
            ValidateStepId(stepId);

            return $"{Prefix}:claim:{executionId}:{stepId}";
        }

        /// <inheritdoc />
        public string GetDagLeaseKey(string executionId, string stepId)
        {
            ValidateExecutionId(executionId);
            ValidateStepId(stepId);

            return $"{Prefix}:lease:{executionId}:{stepId}";
        }

        /// <inheritdoc />
        public string GetDagInFlightKey(string executionId)
        {
            ValidateExecutionId(executionId);
            return $"{Prefix}:inflight:{executionId}";
        }

        /// <inheritdoc />
        public string GetDagMetaKey(string executionId)
        {
            ValidateExecutionId(executionId);
            return $"{Prefix}:meta:{executionId}";
        }

        private static void ValidateExecutionId(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));
        }

        private static void ValidateStepId(string stepId)
        {
            if (string.IsNullOrWhiteSpace(stepId))
                throw new ArgumentException("Step id cannot be null or empty.", nameof(stepId));
        }
    }
}