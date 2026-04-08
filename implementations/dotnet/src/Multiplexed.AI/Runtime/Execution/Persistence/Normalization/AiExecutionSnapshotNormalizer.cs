using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Steps;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Normalization
{
    /// <summary>
    /// Normalizes execution snapshot data into a persistence-safe form.
    /// This removes JsonElement instances by converting them into plain CLR values.
    /// </summary>
    public static class AiExecutionSnapshotNormalizer
    {
        /// <summary>
        /// Normalizes the provided snapshot in place.
        /// </summary>
        public static AiExecutionSnapshotDocument<TContext>? Normalize<TContext>(AiExecutionSnapshotDocument<TContext>? snapshot)
        {
            if (snapshot is  not null)
            {
                NormalizeState(snapshot.State);
            }

            
            return snapshot;
        }

        private static void NormalizeState(AiExecutionState? state)
        {
            if (state is null)
            {
                return;
            }

            state.Data = NormalizeDictionary(state.Data);
            state.Metadata = NormalizeDictionary(state.Metadata);

            if (state.Steps is null || state.Steps.Count == 0)
            {
                return;
            }

            foreach (var step in state.Steps)
            {
                NormalizeStep(step.Value);
            }
        }

        private static void NormalizeStep(AiStepState? step)
        {
            if (step is null)
            {
                return;
            }

            step.Inputs = NormalizeDictionary(step.Inputs);
            step.Config = NormalizeDictionary(step.Config);

            NormalizeStepResult(step.Result);
        }

        private static void NormalizeStepResult(AiStepResult? result)
        {
            if (result is null)
            {
                return;
            }

            result.Data = NormalizeDictionary(result.Data);
        }

        /// <summary>
        /// Normalizes a dictionary recursively and returns an empty dictionary when null.
        /// </summary>
        private static Dictionary<string, object?> NormalizeDictionary(Dictionary<string, object?>? value)
        {
            if (value is null || value.Count == 0)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            var normalized = new Dictionary<string, object?>(value.Count, StringComparer.Ordinal);

            foreach (var pair in value)
            {
                normalized[pair.Key] = NormalizeObject(pair.Value);
            }

            return normalized;
        }

        /// <summary>
        /// Recursively normalizes an arbitrary object graph.
        /// JsonElement values are converted into plain CLR types.
        /// </summary>
        private static object? NormalizeObject(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is JsonElement jsonElement)
            {
                return NormalizeJsonElement(jsonElement);
            }

            if (value is Dictionary<string, object?> dictionary)
            {
                return NormalizeDictionary(dictionary);
            }

            if (value is IEnumerable<object?> enumerable && value is not string)
            {
                var list = new List<object?>();

                foreach (var item in enumerable)
                {
                    list.Add(NormalizeObject(item));
                }

                return list;
            }

            if (value is IEnumerable<object> nonNullableEnumerable && value is not string)
            {
                var list = new List<object?>();

                foreach (var item in nonNullableEnumerable)
                {
                    list.Add(NormalizeObject(item));
                }

                return list;
            }

            return value;
        }

        /// <summary>
        /// Converts a JsonElement into plain CLR values.
        /// </summary>
        private static object? NormalizeJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

                        foreach (var property in element.EnumerateObject())
                        {
                            result[property.Name] = NormalizeJsonElement(property.Value);
                        }

                        return result;
                    }

                case JsonValueKind.Array:
                    {
                        var result = new List<object?>();

                        foreach (var item in element.EnumerateArray())
                        {
                            result.Add(NormalizeJsonElement(item));
                        }

                        return result;
                    }

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var int32))
                    {
                        return int32;
                    }

                    if (element.TryGetInt64(out var int64))
                    {
                        return int64;
                    }

                    if (element.TryGetDecimal(out var decimalValue))
                    {
                        return decimalValue;
                    }

                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return doubleValue;
                    }

                    return element.GetRawText();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;

                default:
                    return element.GetRawText();
            }
        }
    }
}