namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Defines well-known keys used by the AI execution runtime.
    ///
    /// These keys centralize shared execution data and metadata names in order to:
    /// - avoid magic strings
    /// - reduce key mismatches across steps
    /// - improve discoverability and refactoring safety
    /// </summary>
    public static class AiExecutionKeys
    {
        /// <summary>
        /// The default input key used to seed a new execution.
        /// </summary>
        public const string Input = "input";

        /// <summary>
        /// The default summary output key used by summary-oriented steps.
        /// </summary>
        public const string Summary = "summary";

        /// <summary>
        /// The metadata key used to store per-step execution metadata
        /// such as retry attempts, timestamps, and execution status.
        /// </summary>
        public const string StepExecutionMetadata = "ai.steps.execution";
    }
}