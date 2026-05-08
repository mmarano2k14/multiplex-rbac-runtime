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
        public List<string> Policies { get; set; } = [];

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
    }
}