namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Defines runtime-level concurrency configuration.
    /// </summary>
    public sealed class AiConcurrencyOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether concurrency enforcement is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default concurrency definition applied when no step-specific definition exists.
        /// </summary>
        public AiConcurrencyDefinition DefaultDefinition { get; set; } = new();

        /// <summary>
        /// Gets or sets the default delay suggested when concurrency acquisition is denied.
        /// </summary>
        public TimeSpan DefaultRetryAfter { get; set; } = TimeSpan.FromMilliseconds(250);
    }
}