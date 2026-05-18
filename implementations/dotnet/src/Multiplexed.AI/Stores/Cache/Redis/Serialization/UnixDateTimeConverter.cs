using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiplexed.AI.Stores.Cache.Redis.Serialization
{
    /// <summary>
    /// Serializes <see cref="DateTime"/> as Unix time in milliseconds.
    ///
    /// Backward-compatible read behavior:
    /// - accepts unix milliseconds as JSON number
    /// - accepts unix milliseconds as JSON string
    /// - accepts ISO-8601 date string
    /// </summary>
    public sealed class UnixDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
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
                    throw new JsonException("DateTime string value was null or empty.");
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

                throw new JsonException($"Unable to parse DateTime value '{raw}'.");
            }

            throw new JsonException(
                $"Unsupported token type '{reader.TokenType}' for DateTime.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime value,
            JsonSerializerOptions options)
        {
            var utc = value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            var milliseconds = new DateTimeOffset(utc).ToUnixTimeMilliseconds();
            writer.WriteNumberValue(milliseconds);
        }
    }
}