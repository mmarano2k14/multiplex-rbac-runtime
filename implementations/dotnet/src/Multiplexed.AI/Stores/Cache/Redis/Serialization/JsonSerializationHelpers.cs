using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Multiplexed.AI.Stores.Cache.Redis.Serialization
{
    public static class JsonSerializationHelpers
    {
        /// <summary>
        /// Repairs legacy or Lua-corrupted step JSON before deserializing into <see cref="AiStepState"/>.
        /// Specifically ensures DependsOn is always a JSON array.
        /// </summary>
        public static string RepairStepJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                var hasDependsOn = false;

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("DependsOn"))
                    {
                        hasDependsOn = true;
                        writer.WritePropertyName("DependsOn");

                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            property.Value.WriteTo(writer);
                        }
                        else
                        {
                            writer.WriteStartArray();
                            writer.WriteEndArray();
                        }
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                if (!hasDependsOn)
                {
                    writer.WritePropertyName("DependsOn");
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Repairs legacy or incompatible execution record JSON before deserializing into <see cref="AiExecutionRecord"/>.
        /// Specifically ensures CompletedSteps is always a JSON array.
        /// </summary>
        public static string RepairRecordJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                var hasCompletedSteps = false;

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("CompletedSteps"))
                    {
                        hasCompletedSteps = true;
                        writer.WritePropertyName("CompletedSteps");

                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            property.Value.WriteTo(writer);
                        }
                        else
                        {
                            writer.WriteStartArray();
                            writer.WriteEndArray();
                        }
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                if (!hasCompletedSteps)
                {
                    writer.WritePropertyName("CompletedSteps");
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Writes a repaired Retry JSON object where Policies is guaranteed to be a JSON array.
        /// </summary>
        public static void WriteRepairedRetryJson(
            Utf8JsonWriter writer,
            JsonElement retry)
        {
            writer.WriteStartObject();

            var hasPolicies = false;

            foreach (var property in retry.EnumerateObject())
            {
                if (property.NameEquals("Policies"))
                {
                    hasPolicies = true;
                    writer.WritePropertyName("Policies");

                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        property.Value.WriteTo(writer);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue(property.Value.GetString());
                        writer.WriteEndArray();
                    }
                    else
                    {
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                    }

                    continue;
                }

                property.WriteTo(writer);
            }

            if (!hasPolicies)
            {
                writer.WritePropertyName("Policies");
                writer.WriteStartArray();
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Repairs legacy or incompatible step JSON before deserializing into <see cref="AiStepState"/>.
        /// Specifically ensures Retry.Policies is always a JSON array.
        /// </summary>
        public static string RepairRetryJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("Retry"))
                    {
                        writer.WritePropertyName("Retry");

                        if (property.Value.ValueKind == JsonValueKind.Object)
                        {
                            JsonSerializationHelpers.WriteRepairedRetryJson(writer, property.Value);
                        }
                        else
                        {
                            property.Value.WriteTo(writer);
                        }

                        continue;
                    }

                    property.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
