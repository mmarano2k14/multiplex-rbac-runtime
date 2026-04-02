using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiplexed.AI.Stores
{
    /// <summary>
    /// Serializes nullable <see cref="DateTime"/> as Unix time in milliseconds.
    ///
    /// Backward-compatible read behavior:
    /// - accepts null
    /// - accepts unix milliseconds as JSON number
    /// - accepts unix milliseconds as JSON string
    /// - accepts ISO-8601 date string
    /// </summary>
    public sealed class NullableUnixDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                var milliseconds = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var raw = reader.GetString();

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
                }

                if (DateTime.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsedDateTime))
                {
                    return DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc);
                }

                throw new JsonException($"Unable to parse nullable DateTime value '{raw}'.");
            }

            throw new JsonException(
                $"Unsupported token type '{reader.TokenType}' for nullable DateTime.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime? value,
            JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            var utc = value.Value.Kind == DateTimeKind.Utc
                ? value.Value
                : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

            var milliseconds = new DateTimeOffset(utc).ToUnixTimeMilliseconds();
            writer.WriteNumberValue(milliseconds);
        }
    }
}