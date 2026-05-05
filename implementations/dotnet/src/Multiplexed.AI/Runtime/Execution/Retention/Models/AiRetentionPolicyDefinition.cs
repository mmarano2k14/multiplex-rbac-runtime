namespace Multiplexed.AI.Runtime.Execution.Retention.Models
{
    /// <summary>
    /// Defines config-driven retention settings resolved by the retention engine.
    /// </summary>
    /// <remarks>
    /// This configuration may be resolved from pipeline-level config, step-level config,
    /// or a merged configuration where step values override pipeline defaults.
    ///
    /// Missing fields must remain safe and should not crash retention evaluation.
    /// </remarks>
    public sealed class AiRetentionPolicyDefinition
    {
        /// <summary>
        /// Gets a value indicating whether retention is enabled.
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        /// Gets the ordered retention policy keys to evaluate.
        /// </summary>
        public IReadOnlyCollection<string> Policies { get; init; } =
        [
            "retention.compact.terminal"
        ];

        /// <summary>
        /// Gets the trigger configuration used to determine whether retention should run.
        /// </summary>
        public AiRetentionTriggerDefinition Trigger { get; init; } = new();

        /// <summary>
        /// Gets the reason used when archived payload index entries are created.
        /// </summary>
        public string ArchiveReason { get; init; } = "retention";
    }
}