using System;
using System.Text.Json;

namespace Multiplexed.AI.Tests.Integration.Helpers
{
    internal static class AiResultDataAssertions
    {
        public static int ExtractInt(object? value, string fieldName)
        {
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.Number &&
                    json.TryGetInt32(out var jsonInt))
                {
                    return jsonInt;
                }

                if (json.ValueKind == JsonValueKind.String &&
                    int.TryParse(json.GetString(), out var parsed))
                {
                    return parsed;
                }
            }

            throw new InvalidOperationException(
                $"Could not extract int field '{fieldName}' from value of type '{value?.GetType().FullName ?? "null"}'.");
        }

        public static string ExtractString(object? value, string fieldName)
        {
            if (value is string text)
            {
                return text;
            }

            if (value is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.String)
                {
                    return json.GetString()
                        ?? throw new InvalidOperationException($"Field '{fieldName}' was null.");
                }

                return json.ToString();
            }

            throw new InvalidOperationException(
                $"Could not extract string field '{fieldName}' from value of type '{value?.GetType().FullName ?? "null"}'.");
        }

        public static bool ExtractBool(object? value, string fieldName)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (json.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (json.ValueKind == JsonValueKind.String &&
                    bool.TryParse(json.GetString(), out var parsed))
                {
                    return parsed;
                }
            }

            throw new InvalidOperationException(
                $"Could not extract bool field '{fieldName}' from value of type '{value?.GetType().FullName ?? "null"}'.");
        }
    }
}