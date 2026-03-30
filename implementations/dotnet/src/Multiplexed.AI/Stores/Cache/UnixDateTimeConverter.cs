using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiplexed.AI.Stores.Cache
{
    public sealed class UnixDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                var unix = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new JsonException("DateTime value is null or empty.");
                }

                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                }

                if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
                {
                    return parsed.ToUniversalTime();
                }

                throw new JsonException($"Invalid DateTime value '{value}'.");
            }

            throw new JsonException(
                $"Unexpected token {reader.TokenType} when parsing DateTime.");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeSeconds());
        }
    }

    public sealed class NullableUnixDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                var unix = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();

                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                }

                if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
                {
                    return parsed.ToUniversalTime();
                }

                throw new JsonException($"Invalid nullable DateTime value '{value}'.");
            }

            throw new JsonException(
                $"Unexpected token {reader.TokenType} when parsing nullable DateTime.");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteNumberValue(new DateTimeOffset(value.Value.ToUniversalTime()).ToUnixTimeSeconds());
        }
    }

    internal sealed class NullableTimeSpanSecondsConverter : JsonConverter<TimeSpan?>
    {
        public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            return TimeSpan.FromSeconds(reader.GetDouble());
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteNumberValue(value.Value.TotalSeconds);
        }
    }
}