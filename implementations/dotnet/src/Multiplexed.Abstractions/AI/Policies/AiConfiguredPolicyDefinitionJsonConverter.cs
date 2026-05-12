using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiplexed.Abstractions.AI.Policies
{
    /// <summary>
    /// Supports backward-compatible policy definition deserialization.
    /// </summary>
    /// <remarks>
    /// Supported JSON formats:
    ///
    /// Legacy string format:
    ///
    /// <code>
    /// "policies": [
    ///   "retry.transient.default"
    /// ]
    /// </code>
    ///
    /// Structured object format:
    ///
    /// <code>
    /// "policies": [
    ///   {
    ///     "name": "concurrency.scope.default",
    ///     "type": "scope",
    ///     "config": {
    ///       "kind": "provider",
    ///       "value": "openai",
    ///       "limit": 10
    ///     }
    ///   }
    /// ]
    /// </code>
    /// </remarks>
    public sealed class AiConfiguredPolicyDefinitionJsonConverter
        : JsonConverter<AiConfiguredPolicyDefinition>
    {
        /// <inheritdoc />
        public override AiConfiguredPolicyDefinition Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var policyName = reader.GetString();

                if (string.IsNullOrWhiteSpace(policyName))
                {
                    throw new JsonException(
                        "Policy name cannot be null or empty.");
                }

                return new AiConfiguredPolicyDefinition
                {
                    Name = policyName
                };
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(
                    $"Unexpected token '{reader.TokenType}' while reading AI policy definition.");
            }

            using var document = JsonDocument.ParseValue(ref reader);

            var root = document.RootElement;

            var result = new AiConfiguredPolicyDefinition();

            if (root.TryGetProperty("name", out var nameElement))
            {
                result.Name = nameElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("type", out var typeElement))
            {
                result.Type = typeElement.GetString();
            }

            if (root.TryGetProperty("config", out var configElement))
            {
                result.Config =
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        configElement.GetRawText(),
                        options)
                    ?? new Dictionary<string, object?>();
            }

            if (string.IsNullOrWhiteSpace(result.Name))
            {
                throw new JsonException(
                    "AI configured policy definition requires a policy name.");
            }

            return result;
        }

        /// <inheritdoc />
        public override void Write(
            Utf8JsonWriter writer,
            AiConfiguredPolicyDefinition value,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStartObject();

            writer.WriteString(
                "name",
                value.Name);

            if (!string.IsNullOrWhiteSpace(value.Type))
            {
                writer.WriteString(
                    "type",
                    value.Type);
            }

            writer.WritePropertyName("config");

            JsonSerializer.Serialize(
                writer,
                value.Config,
                options);

            writer.WriteEndObject();
        }
    }
}