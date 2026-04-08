using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Steps;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Normalization
{
    /// <summary>
    /// Rebuilds snapshot values after persistence by remapping CLR-safe values
    /// back into runtime-friendly JSON representations where needed.
    /// </summary>
    public static class AiExecutionSnapshotRemapper
    {
        /// <summary>
        /// Remaps the provided snapshot in place.
        /// </summary>
        public static void Remap<TContext>(AiExecutionSnapshotDocument<TContext>? snapshot)
        {
            if (snapshot is null)
            {
                return;
            }

            RemapState(snapshot.State);
        }

        private static void RemapState(AiExecutionState? state)
        {
            if (state is null)
            {
                return;
            }

            state.Data = RemapDictionary(state.Data);
            state.Metadata = RemapDictionary(state.Metadata);

            if (state.Steps is null || state.Steps.Count == 0)
            {
                return;
            }

            foreach (var step in state.Steps)
            {
                RemapStep(step.Value);
            }
        }

        private static void RemapStep(AiStepState? step)
        {
            if (step is null)
            {
                return;
            }

            step.Inputs = RemapDictionary(step.Inputs);
            step.Config = RemapDictionary(step.Config);

            RemapStepResult(step.Result);
        }

        private static void RemapStepResult(AiStepResult? result)
        {
            if (result is null)
            {
                return;
            }

            result.Data = RemapDictionary(result.Data);
        }

        /// <summary>
        /// Remaps a dictionary recursively and returns an empty dictionary when null.
        /// </summary>
        private static Dictionary<string, object?> RemapDictionary(Dictionary<string, object?>? value)
        {
            if (value is null || value.Count == 0)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            var remapped = new Dictionary<string, object?>(value.Count, StringComparer.Ordinal);

            foreach (var pair in value)
            {
                remapped[pair.Key] = RemapObject(pair.Value);
            }

            return remapped;
        }

        /// <summary>
        /// Recursively remaps CLR-safe persistence values back into runtime-friendly values.
        /// Dictionaries and lists are converted to JsonElement.
        /// Primitive values are left unchanged.
        /// </summary>
        private static object? RemapObject(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is JsonElement)
            {
                return value;
            }

            if (value is Dictionary<string, object?> dictionary)
            {
                return ToJsonElement(dictionary);
            }

            if (value is IEnumerable<object?> enumerable && value is not string)
            {
                var list = new List<object?>();

                foreach (var item in enumerable)
                {
                    list.Add(RemappedCollectionItem(item));
                }

                return ToJsonElement(list);
            }

            if (value is IEnumerable<object> nonNullableEnumerable && value is not string)
            {
                var list = new List<object?>();

                foreach (var item in nonNullableEnumerable)
                {
                    list.Add(RemappedCollectionItem(item));
                }

                return ToJsonElement(list);
            }

            return value;
        }

        /// <summary>
        /// Recursively prepares nested values before JsonElement reconstruction.
        /// </summary>
        private static object? RemappedCollectionItem(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is JsonElement)
            {
                return value;
            }

            if (value is Dictionary<string, object?> dictionary)
            {
                var rebuilt = new Dictionary<string, object?>(dictionary.Count, StringComparer.Ordinal);

                foreach (var pair in dictionary)
                {
                    rebuilt[pair.Key] = RemappedCollectionItem(pair.Value);
                }

                return rebuilt;
            }

            if (value is IEnumerable<object?> enumerable && value is not string)
            {
                var rebuilt = new List<object?>();

                foreach (var item in enumerable)
                {
                    rebuilt.Add(RemappedCollectionItem(item));
                }

                return rebuilt;
            }

            if (value is IEnumerable<object> nonNullableEnumerable && value is not string)
            {
                var rebuilt = new List<object?>();

                foreach (var item in nonNullableEnumerable)
                {
                    rebuilt.Add(RemappedCollectionItem(item));
                }

                return rebuilt;
            }

            return value;
        }

        /// <summary>
        /// Converts any CLR value into a detached JsonElement.
        /// </summary>
        private static JsonElement ToJsonElement(object? value)
        {
            var json = JsonSerializer.Serialize(value);
            using var document = JsonDocument.Parse(json);

            return document.RootElement.Clone();
        }
    }
}