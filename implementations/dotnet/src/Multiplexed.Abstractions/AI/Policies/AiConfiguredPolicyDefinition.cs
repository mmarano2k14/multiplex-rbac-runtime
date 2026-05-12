using System.Text.Json.Serialization;

namespace Multiplexed.Abstractions.AI.Policies
{
    /// <summary>
    /// Defines a configured AI policy entry.
    /// </summary>
    [JsonConverter(typeof(AiConfiguredPolicyDefinitionJsonConverter))]
    public sealed class AiConfiguredPolicyDefinition
    {
        /// <summary>
        /// Gets or sets the registered policy name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional policy type or category.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets policy-specific configuration.
        /// </summary>
        public Dictionary<string, object?> Config { get; set; } = new();
    }
}