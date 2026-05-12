using Multiplexed.Abstractions.AI.Policies;

namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Defines concurrency configuration and throttling policies for an AI step.
    /// </summary>
    public sealed class AiConcurrencyDefinition
    {
        /// <summary>
        /// Gets or sets a value indicating whether concurrency enforcement is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the ordered concurrency policy keys evaluated by the runtime.
        /// </summary>
        public List<AiConfiguredPolicyDefinition> Policies { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum number of concurrently executing steps
        /// across the entire runtime.
        /// </summary>
        public int? MaxGlobalConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrently executing steps
        /// for the same pipeline.
        /// </summary>
        public int? MaxPipelineConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrently executing steps
        /// for the same step key.
        /// </summary>
        public int? MaxStepConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrently executing steps
        /// within the same execution.
        /// </summary>
        public int? MaxExecutionConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrently executing steps
        /// for the same runtime instance.
        /// </summary>
        public int? MaxInstanceConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the default retry delay in milliseconds when concurrency acquisition is denied.
        /// </summary>
        public int DefaultRetryAfterMs { get; set; } = 250;

        /// <summary>
        /// Gets or sets the concurrency lease duration in seconds.
        /// </summary>
        public int LeaseSeconds { get; set; } = 300;


        /// <summary>
        /// Gets or sets the maximum local degree of parallelism used for
        /// already-claimed DAG step execution inside the current runtime instance.
        /// </summary>
        /// <remarks>
        /// This setting controls local bounded parallel execution only.
        /// It does not control distributed admission or global throttling.
        /// </remarks>
        public int? MaxDegreeOfParallelism { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether retry-after jitter should be applied
        /// when concurrency acquisition is denied.
        /// </summary>
        /// <remarks>
        /// This helps avoid multiple runtime instances retrying admission at exactly
        /// the same time after being throttled.
        /// </remarks>
        public bool Jitter { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum retry-after jitter in milliseconds.
        /// </summary>
        public int MaxJitterMs { get; set; } = 100;
    }
}