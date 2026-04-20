using System.Globalization;
using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using Multiplexed.AI.Runtime.Execution.Normalization;

namespace Multiplexed.AI.Runtime.AI.Rag.Normalization
{
    /// <summary>
    /// Restores strong typing for RAG-related step result payloads after deserialization.
    ///
    /// PURPOSE:
    /// - Rehydrates RAG retrieval batches, composed contexts, and fragment collections.
    /// - Ensures downstream steps receive typed values instead of raw JsonElement or dictionaries.
    /// - Keeps RAG step consumers simple and test-friendly.
    ///
    /// DESIGN:
    /// - This normalizer is module-specific and RAG-focused.
    /// - It is safe to run multiple times.
    /// - It leaves already-typed values untouched.
    ///
    /// IMPORTANT:
    /// - This normalizer must not change execution truth, only its in-memory representation.
    /// - It must be tolerant of partially serialized or partially typed result payloads.
    /// - It is intended to run after distributed state is loaded from storage.
    /// </summary>
    public sealed class RagStepResultNormalizer : IAiStepResultNormalizer
    {
        /// <inheritdoc />
        public void Normalize(AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            if (state.Steps is null || state.Steps.Count == 0)
            {
                return;
            }

            foreach (var step in state.Steps.Values)
            {
                NormalizeStep(step);
            }
        }

        /// <summary>
        /// Normalizes the result payload of a single step when present.
        ///
        /// PURPOSE:
        /// - Applies all RAG-specific post-load rehydration rules to a step result.
        /// - Keeps the normalization surface centralized and idempotent.
        /// </summary>
        private static void NormalizeStep(AiStepState step)
        {
            if (step.Result?.Data is null || step.Result.Data.Count == 0)
            {
                return;
            }

            NormalizeBatch(step.Result.Data);
            NormalizeContext(step.Result.Data);
            NormalizeFragments(step.Result.Data);
        }

        /// <summary>
        /// Restores a strongly typed <see cref="RagRetrievalBatch"/> when a batch entry exists.
        /// </summary>
        private static void NormalizeBatch(IDictionary<string, object?> data)
        {
            if (!TryGetValue(data, "batch", out var raw) || raw is null)
            {
                return;
            }

            if (raw is RagRetrievalBatch)
            {
                return;
            }

            if (TryConvertToRetrievalBatch(raw, out var batch))
            {
                data["batch"] = batch;
            }
        }

        /// <summary>
        /// Restores a strongly typed <see cref="RagStructuredContext"/> when a context entry exists.
        /// </summary>
        private static void NormalizeContext(IDictionary<string, object?> data)
        {
            if (!TryGetValue(data, "context", out var raw) || raw is null)
            {
                return;
            }

            if (raw is RagStructuredContext)
            {
                return;
            }

            if (TryConvertToStructuredContext(raw, out var context))
            {
                data["context"] = context;
            }
        }

        /// <summary>
        /// Restores a strongly typed fragment list when a fragments entry exists.
        /// </summary>
        private static void NormalizeFragments(IDictionary<string, object?> data)
        {
            if (!TryGetValue(data, "fragments", out var raw) || raw is null)
            {
                return;
            }

            if (raw is IReadOnlyList<RagContextFragment>)
            {
                return;
            }

            if (TryConvertToFragments(raw, out var fragments))
            {
                data["fragments"] = fragments;
            }
        }

        // ---------------------------------------------------------------------
        // RETRIEVAL BATCH CONVERSION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to convert a runtime value into a strongly typed <see cref="RagRetrievalBatch"/>.
        ///
        /// PURPOSE:
        /// - Rehydrates retrieval step outputs after crossing serialization boundaries.
        /// - Supports both typed and loosely typed representations.
        /// </summary>
        private static bool TryConvertToRetrievalBatch(
            object raw,
            out RagRetrievalBatch batch)
        {
            if (raw is RagRetrievalBatch typedBatch)
            {
                batch = typedBatch;
                return true;
            }

            if (raw is JsonElement json)
            {
                return TryConvertJsonElementToRetrievalBatch(json, out batch);
            }

            if (raw is IDictionary<string, object?> dict)
            {
                return TryConvertDictionaryToRetrievalBatch(dict, out batch);
            }

            batch = default!;
            return false;
        }

        /// <summary>
        /// Attempts to convert a JSON representation into a strongly typed retrieval batch.
        /// </summary>
        private static bool TryConvertJsonElementToRetrievalBatch(
            JsonElement json,
            out RagRetrievalBatch batch)
        {
            try
            {
                var items = new List<RagNormalizedItem>();

                if (json.ValueKind == JsonValueKind.Object &&
                    json.TryGetProperty("Items", out var itemsProperty) &&
                    itemsProperty.ValueKind == JsonValueKind.Array)
                {
                    foreach (var itemElement in itemsProperty.EnumerateArray())
                    {
                        if (TryConvertJsonElementToNormalizedItem(itemElement, out var item))
                        {
                            items.Add(item);
                        }
                    }
                }

                RagRetrievalDiagnostics? diagnostics = null;

                if (json.ValueKind == JsonValueKind.Object &&
                    json.TryGetProperty("Diagnostics", out var diagnosticsProperty) &&
                    diagnosticsProperty.ValueKind == JsonValueKind.Object)
                {
                    diagnostics = ConvertJsonElementToDiagnostics(diagnosticsProperty);
                }

                batch = new RagRetrievalBatch
                {
                    Items = items,
                    Diagnostics = diagnostics
                };

                return true;
            }
            catch
            {
                batch = default!;
                return false;
            }
        }

        /// <summary>
        /// Attempts to convert a dictionary representation into a strongly typed retrieval batch.
        /// </summary>
        private static bool TryConvertDictionaryToRetrievalBatch(
            IDictionary<string, object?> dict,
            out RagRetrievalBatch batch)
        {
            try
            {
                var items = new List<RagNormalizedItem>();

                if (TryGetValue(dict, "Items", out var rawItems) &&
                    rawItems is IEnumerable<object?> itemObjects)
                {
                    foreach (var rawItem in itemObjects)
                    {
                        if (rawItem is null)
                        {
                            continue;
                        }

                        if (rawItem is RagNormalizedItem typedItem)
                        {
                            items.Add(typedItem);
                            continue;
                        }

                        if (rawItem is IDictionary<string, object?> itemDict &&
                            TryConvertDictionaryToNormalizedItem(itemDict, out var convertedItem))
                        {
                            items.Add(convertedItem);
                            continue;
                        }

                        if (rawItem is JsonElement itemJson &&
                            TryConvertJsonElementToNormalizedItem(itemJson, out var jsonItem))
                        {
                            items.Add(jsonItem);
                        }
                    }
                }

                RagRetrievalDiagnostics? diagnostics = null;

                if (TryGetValue(dict, "Diagnostics", out var rawDiagnostics) &&
                    rawDiagnostics is not null &&
                    TryConvertDiagnostics(rawDiagnostics, out var convertedDiagnostics))
                {
                    diagnostics = convertedDiagnostics;
                }

                batch = new RagRetrievalBatch
                {
                    Items = items,
                    Diagnostics = diagnostics
                };

                return true;
            }
            catch
            {
                batch = default!;
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // STRUCTURED CONTEXT CONVERSION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to convert a runtime value into a strongly typed <see cref="RagStructuredContext"/>.
        ///
        /// PURPOSE:
        /// - Restores a composed context after serialization boundaries.
        /// - Supports both strongly typed and loosely typed runtime representations.
        /// - Avoids relying on direct JSON deserialization for interface-heavy models.
        ///
        /// IMPORTANT:
        /// - This method is intentionally tolerant.
        /// - It accepts:
        ///   - <see cref="RagStructuredContext"/>
        ///   - <see cref="JsonElement"/>
        ///   - <see cref="IDictionary{TKey,TValue}"/>
        /// - It must not throw for normal malformed runtime payloads.
        /// </summary>
        private static bool TryConvertToStructuredContext(
            object raw,
            out RagStructuredContext context)
        {
            if (raw is RagStructuredContext typed)
            {
                context = typed;
                return true;
            }

            if (raw is JsonElement json)
            {
                return TryConvertJsonElementToStructuredContext(json, out context);
            }

            if (raw is IDictionary<string, object?> dict)
            {
                return TryConvertDictionaryToStructuredContext(dict, out context);
            }

            context = default!;
            return false;
        }

        /// <summary>
        /// Attempts to convert a JSON representation into a strongly typed <see cref="RagStructuredContext"/>.
        ///
        /// PURPOSE:
        /// - Rehydrates composed context values from JSON objects.
        /// - Handles interface-based properties explicitly and deterministically.
        /// </summary>
        private static bool TryConvertJsonElementToStructuredContext(
            JsonElement json,
            out RagStructuredContext context)
        {
            try
            {
                if (json.ValueKind != JsonValueKind.Object)
                {
                    context = default!;
                    return false;
                }

                var text = GetJsonString(json, "Text") ?? string.Empty;
                var orderedTexts = ExtractJsonStringArray(json, "OrderedTexts");
                var groups = ExtractJsonGroupedStrings(json, "Groups");

                context = new RagStructuredContext
                {
                    Text = text,
                    OrderedTexts = orderedTexts,
                    Groups = groups
                };

                return true;
            }
            catch
            {
                context = default!;
                return false;
            }
        }

        /// <summary>
        /// Attempts to convert a dictionary representation into a strongly typed <see cref="RagStructuredContext"/>.
        ///
        /// PURPOSE:
        /// - Rehydrates composed context values from loosely typed runtime dictionaries.
        /// - Handles interface-based properties explicitly and deterministically.
        /// </summary>
        private static bool TryConvertDictionaryToStructuredContext(
            IDictionary<string, object?> dict,
            out RagStructuredContext context)
        {
            try
            {
                var text = GetDictionaryString(dict, "Text") ?? string.Empty;
                var orderedTexts = ExtractStringArray(dict, "OrderedTexts");
                var groups = ExtractGroupedStrings(dict, "Groups");

                context = new RagStructuredContext
                {
                    Text = text,
                    OrderedTexts = orderedTexts,
                    Groups = groups
                };

                return true;
            }
            catch
            {
                context = default!;
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // FRAGMENT CONVERSION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to convert a runtime value into a strongly typed fragment list.
        ///
        /// PURPOSE:
        /// - Restores fragment collections after serialization boundaries.
        /// - Supports both strongly typed and loosely typed runtime representations.
        ///
        /// IMPORTANT:
        /// - This method must remain tolerant and idempotent.
        /// </summary>
        private static bool TryConvertToFragments(
            object raw,
            out IReadOnlyList<RagContextFragment> fragments)
        {
            if (raw is IReadOnlyList<RagContextFragment> typedReadOnly)
            {
                fragments = typedReadOnly;
                return true;
            }

            if (raw is IEnumerable<RagContextFragment> typedEnumerable)
            {
                fragments = typedEnumerable.ToArray();
                return true;
            }

            if (raw is JsonElement json)
            {
                if (json.ValueKind != JsonValueKind.Array)
                {
                    fragments = Array.Empty<RagContextFragment>();
                    return false;
                }

                try
                {
                    var result = new List<RagContextFragment>();

                    foreach (var item in json.EnumerateArray())
                    {
                        if (TryConvertJsonElementToFragment(item, out var fragment))
                        {
                            result.Add(fragment);
                        }
                    }

                    fragments = result;
                    return true;
                }
                catch
                {
                    fragments = Array.Empty<RagContextFragment>();
                    return false;
                }
            }

            if (raw is IEnumerable<object?> objectItems)
            {
                try
                {
                    var result = new List<RagContextFragment>();

                    foreach (var rawItem in objectItems)
                    {
                        if (rawItem is RagContextFragment typedFragment)
                        {
                            result.Add(typedFragment);
                            continue;
                        }

                        if (rawItem is IDictionary<string, object?> fragmentDict &&
                            TryConvertDictionaryToFragment(fragmentDict, out var dictFragment))
                        {
                            result.Add(dictFragment);
                            continue;
                        }

                        if (rawItem is JsonElement fragmentJson &&
                            TryConvertJsonElementToFragment(fragmentJson, out var jsonFragment))
                        {
                            result.Add(jsonFragment);
                        }
                    }

                    fragments = result;
                    return true;
                }
                catch
                {
                    fragments = Array.Empty<RagContextFragment>();
                    return false;
                }
            }

            fragments = Array.Empty<RagContextFragment>();
            return false;
        }

        // ---------------------------------------------------------------------
        // DIAGNOSTICS CONVERSION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to convert a runtime value into strongly typed retrieval diagnostics.
        /// </summary>
        private static bool TryConvertDiagnostics(
            object raw,
            out RagRetrievalDiagnostics diagnostics)
        {
            if (raw is RagRetrievalDiagnostics typed)
            {
                diagnostics = typed;
                return true;
            }

            if (raw is JsonElement json)
            {
                try
                {
                    diagnostics = ConvertJsonElementToDiagnostics(json);
                    return true;
                }
                catch
                {
                    diagnostics = default!;
                    return false;
                }
            }

            if (raw is IDictionary<string, object?> dict)
            {
                try
                {
                    diagnostics = ConvertDictionaryToDiagnostics(dict);
                    return true;
                }
                catch
                {
                    diagnostics = default!;
                    return false;
                }
            }

            diagnostics = default!;
            return false;
        }

        /// <summary>
        /// Converts a JSON diagnostics object into <see cref="RagRetrievalDiagnostics"/>.
        /// </summary>
        private static RagRetrievalDiagnostics ConvertJsonElementToDiagnostics(JsonElement json)
        {
            var providers = new List<RagProviderExecutionDiagnostics>();

            if (json.TryGetProperty("Providers", out var providersProperty) &&
                providersProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var providerJson in providersProperty.EnumerateArray())
                {
                    providers.Add(new RagProviderExecutionDiagnostics
                    {
                        ProviderKey = GetJsonString(providerJson, "ProviderKey") ?? string.Empty,
                        Success = GetJsonBool(providerJson, "Success"),
                        IsFallbackUsed = GetJsonBool(providerJson, "IsFallbackUsed"),
                        ItemCount = GetJsonInt(providerJson, "ItemCount"),
                        DurationMs = GetJsonLong(providerJson, "DurationMs"),
                        Error = GetJsonString(providerJson, "Error")
                    });
                }
            }

            return new RagRetrievalDiagnostics
            {
                TotalProviders = GetJsonInt(json, "TotalProviders"),
                SuccessfulProviders = GetJsonInt(json, "SuccessfulProviders"),
                FailedProviders = GetJsonInt(json, "FailedProviders"),
                RawItemCount = GetJsonInt(json, "RawItemCount"),
                AfterMergeCount = GetJsonInt(json, "AfterMergeCount"),
                AfterDedupCount = GetJsonInt(json, "AfterDedupCount"),
                FinalItemCount = GetJsonInt(json, "FinalItemCount"),
                TotalDurationMs = GetJsonLong(json, "TotalDurationMs"),
                Providers = providers
            };
        }

        /// <summary>
        /// Converts a dictionary diagnostics object into <see cref="RagRetrievalDiagnostics"/>.
        /// </summary>
        private static RagRetrievalDiagnostics ConvertDictionaryToDiagnostics(
            IDictionary<string, object?> dict)
        {
            var providers = new List<RagProviderExecutionDiagnostics>();

            if (TryGetValue(dict, "Providers", out var rawProviders) &&
                rawProviders is IEnumerable<object?> providerObjects)
            {
                foreach (var providerObject in providerObjects)
                {
                    if (providerObject is RagProviderExecutionDiagnostics typedProvider)
                    {
                        providers.Add(typedProvider);
                        continue;
                    }

                    if (providerObject is IDictionary<string, object?> providerDict)
                    {
                        providers.Add(new RagProviderExecutionDiagnostics
                        {
                            ProviderKey = GetDictionaryString(providerDict, "ProviderKey") ?? string.Empty,
                            Success = GetDictionaryBool(providerDict, "Success"),
                            IsFallbackUsed = GetDictionaryBool(providerDict, "IsFallbackUsed"),
                            ItemCount = GetDictionaryInt(providerDict, "ItemCount"),
                            DurationMs = GetDictionaryLong(providerDict, "DurationMs"),
                            Error = GetDictionaryString(providerDict, "Error")
                        });
                        continue;
                    }

                    if (providerObject is JsonElement providerJson)
                    {
                        providers.Add(new RagProviderExecutionDiagnostics
                        {
                            ProviderKey = GetJsonString(providerJson, "ProviderKey") ?? string.Empty,
                            Success = GetJsonBool(providerJson, "Success"),
                            IsFallbackUsed = GetJsonBool(providerJson, "IsFallbackUsed"),
                            ItemCount = GetJsonInt(providerJson, "ItemCount"),
                            DurationMs = GetJsonLong(providerJson, "DurationMs"),
                            Error = GetJsonString(providerJson, "Error")
                        });
                    }
                }
            }

            return new RagRetrievalDiagnostics
            {
                TotalProviders = GetDictionaryInt(dict, "TotalProviders"),
                SuccessfulProviders = GetDictionaryInt(dict, "SuccessfulProviders"),
                FailedProviders = GetDictionaryInt(dict, "FailedProviders"),
                RawItemCount = GetDictionaryInt(dict, "RawItemCount"),
                AfterMergeCount = GetDictionaryInt(dict, "AfterMergeCount"),
                AfterDedupCount = GetDictionaryInt(dict, "AfterDedupCount"),
                FinalItemCount = GetDictionaryInt(dict, "FinalItemCount"),
                TotalDurationMs = GetDictionaryLong(dict, "TotalDurationMs"),
                Providers = providers
            };
        }

        // ---------------------------------------------------------------------
        // NORMALIZED ITEM CONVERSION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to convert a JSON item representation into <see cref="RagNormalizedItem"/>.
        /// </summary>
        private static bool TryConvertJsonElementToNormalizedItem(
            JsonElement json,
            out RagNormalizedItem item)
        {
            try
            {
                item = new RagNormalizedItem
                {
                    Id = GetJsonString(json, "Id") ?? string.Empty,
                    ProviderKey = GetJsonString(json, "ProviderKey") ?? string.Empty,
                    ProviderKind = GetJsonEnum<RagProviderKind>(json, "ProviderKind"),
                    SourceType = GetJsonEnum<RagProviderSourceType>(json, "SourceType"),
                    RetrievalKey = GetJsonString(json, "RetrievalKey") ?? string.Empty,
                    RetrievalKind = GetJsonEnum<RagRetrievalKind>(json, "RetrievalKind"),
                    ContentType = GetJsonString(json, "ContentType") ?? string.Empty,
                    ContentText = GetJsonString(json, "ContentText"),
                    Score = GetJsonNullableDouble(json, "Score"),
                    StableOrder = GetJsonInt(json, "StableOrder"),
                    Metadata = GetJsonMetadata(json, "Metadata")
                };

                return true;
            }
            catch
            {
                item = default!;
                return false;
            }
        }

        /// <summary>
        /// Attempts to convert a dictionary item representation into <see cref="RagNormalizedItem"/>.
        /// </summary>
        private static bool TryConvertDictionaryToNormalizedItem(
            IDictionary<string, object?> dict,
            out RagNormalizedItem item)
        {
            try
            {
                item = new RagNormalizedItem
                {
                    Id = GetDictionaryString(dict, "Id") ?? string.Empty,
                    ProviderKey = GetDictionaryString(dict, "ProviderKey") ?? string.Empty,
                    ProviderKind = GetDictionaryEnum<RagProviderKind>(dict, "ProviderKind"),
                    SourceType = GetDictionaryEnum<RagProviderSourceType>(dict, "SourceType"),
                    RetrievalKey = GetDictionaryString(dict, "RetrievalKey") ?? string.Empty,
                    RetrievalKind = GetDictionaryEnum<RagRetrievalKind>(dict, "RetrievalKind"),
                    ContentType = GetDictionaryString(dict, "ContentType") ?? string.Empty,
                    ContentText = GetDictionaryString(dict, "ContentText"),
                    Score = GetDictionaryNullableDouble(dict, "Score"),
                    StableOrder = GetDictionaryInt(dict, "StableOrder"),
                    Metadata = GetDictionaryMetadata(dict, "Metadata")
                };

                return true;
            }
            catch
            {
                item = default!;
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // FRAGMENT CONVERSION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to convert a JSON fragment representation into <see cref="RagContextFragment"/>.
        /// </summary>
        private static bool TryConvertJsonElementToFragment(
            JsonElement json,
            out RagContextFragment fragment)
        {
            try
            {
                fragment = new RagContextFragment
                {
                    Key = GetJsonString(json, "Key") ?? string.Empty,
                    FragmentKind = GetJsonEnum<RagFragmentKind>(json, "FragmentKind"),
                    Text = GetJsonString(json, "Text") ?? string.Empty,
                    Order = GetJsonInt(json, "Order"),
                    Score = GetJsonNullableDouble(json, "Score"),
                    SourceIds = ExtractJsonStringArray(json, "SourceIds"),
                    Metadata = GetJsonMetadata(json, "Metadata")
                };

                return true;
            }
            catch
            {
                fragment = default!;
                return false;
            }
        }

        /// <summary>
        /// Attempts to convert a dictionary fragment representation into <see cref="RagContextFragment"/>.
        /// </summary>
        private static bool TryConvertDictionaryToFragment(
            IDictionary<string, object?> dict,
            out RagContextFragment fragment)
        {
            try
            {
                fragment = new RagContextFragment
                {
                    Key = GetDictionaryString(dict, "Key") ?? string.Empty,
                    FragmentKind = GetDictionaryEnum<RagFragmentKind>(dict, "FragmentKind"),
                    Text = GetDictionaryString(dict, "Text") ?? string.Empty,
                    Order = GetDictionaryInt(dict, "Order"),
                    Score = GetDictionaryNullableDouble(dict, "Score"),
                    SourceIds = ExtractStringArray(dict, "SourceIds"),
                    Metadata = GetDictionaryMetadata(dict, "Metadata")
                };

                return true;
            }
            catch
            {
                fragment = default!;
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // SHARED EXTRACTION HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Tries to resolve a dictionary value using exact or case-insensitive matching.
        /// </summary>
        private static bool TryGetValue(
            IDictionary<string, object?> dict,
            string key,
            out object? value)
        {
            if (dict.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (var entry in dict)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Extracts a string array from a dictionary entry.
        /// </summary>
        private static IReadOnlyList<string> ExtractStringArray(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var value) || value is null)
            {
                return Array.Empty<string>();
            }

            if (value is IEnumerable<object?> objects)
            {
                return objects
                    .Select(x => x?.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
            }

            if (value is JsonElement json)
            {
                return ExtractJsonStringArray(json, key: null);
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Extracts grouped string collections from a dictionary entry.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractGroupedStrings(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var raw) || raw is null)
            {
                return new Dictionary<string, IReadOnlyList<string>>();
            }

            if (raw is JsonElement json &&
                json.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

                foreach (var member in json.EnumerateObject())
                {
                    result[member.Name] = ExtractJsonStringArray(member.Value, key: null);
                }

                return result;
            }

            if (raw is IDictionary<string, object?> objectDict)
            {
                var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

                foreach (var entry in objectDict)
                {
                    if (entry.Value is IEnumerable<object?> values)
                    {
                        result[entry.Key] = values
                            .Select(x => x?.ToString())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Cast<string>()
                            .ToArray();
                    }
                    else if (entry.Value is JsonElement entryJson)
                    {
                        result[entry.Key] = ExtractJsonStringArray(entryJson, key: null);
                    }
                }

                return result;
            }

            return new Dictionary<string, IReadOnlyList<string>>();
        }

        /// <summary>
        /// Extracts grouped string collections from a JSON object property.
        ///
        /// PURPOSE:
        /// - Rehydrates <see cref="RagStructuredContext.Groups"/> safely from JSON.
        /// - Preserves deterministic key and item ordering as present in the payload.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractJsonGroupedStrings(
            JsonElement json,
            string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, IReadOnlyList<string>>();
            }

            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            foreach (var member in property.EnumerateObject())
            {
                result[member.Name] = ExtractJsonStringArray(member.Value, key: null);
            }

            return result;
        }

        /// <summary>
        /// Extracts a string array from either a JSON array or a named JSON array property.
        /// </summary>
        private static IReadOnlyList<string> ExtractJsonStringArray(
            JsonElement json,
            string? key)
        {
            JsonElement source = json;

            if (!string.IsNullOrWhiteSpace(key))
            {
                if (json.ValueKind != JsonValueKind.Object ||
                    !json.TryGetProperty(key, out source))
                {
                    return Array.Empty<string>();
                }
            }

            if (source.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return source.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray();
        }

        // ---------------------------------------------------------------------
        // JSON HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Reads a JSON string-like property.
        /// </summary>
        private static string? GetJsonString(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => property.GetRawText()
            };
        }

        /// <summary>
        /// Reads a JSON integer property with tolerant parsing.
        /// </summary>
        private static int GetJsonInt(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out var property))
            {
                return 0;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (property.ValueKind == JsonValueKind.String &&
                int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return 0;
        }

        /// <summary>
        /// Reads a JSON long property with tolerant parsing.
        /// </summary>
        private static long GetJsonLong(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out var property))
            {
                return 0L;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt64(out var longValue))
            {
                return longValue;
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return 0L;
        }

        /// <summary>
        /// Reads a JSON boolean property with tolerant parsing.
        /// </summary>
        private static bool GetJsonBool(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String &&
                bool.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }

            return false;
        }

        /// <summary>
        /// Reads a nullable JSON double property with tolerant parsing.
        /// </summary>
        private static double? GetJsonNullableDouble(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Null ||
                property.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetDouble(out var doubleValue))
            {
                return doubleValue;
            }

            if (property.ValueKind == JsonValueKind.String &&
                double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// Reads a JSON enum property with tolerant parsing.
        /// </summary>
        private static TEnum GetJsonEnum<TEnum>(JsonElement json, string propertyName)
            where TEnum : struct, Enum
        {
            var text = GetJsonString(json, propertyName);

            if (!string.IsNullOrWhiteSpace(text) &&
                Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return default;
        }

        /// <summary>
        /// Reads a metadata dictionary from a JSON object property.
        /// </summary>
        private static IReadOnlyDictionary<string, object?> GetJsonMetadata(
            JsonElement json,
            string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object?>();
            }

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var member in property.EnumerateObject())
            {
                result[member.Name] = ConvertJsonValue(member.Value);
            }

            return result;
        }

        /// <summary>
        /// Converts a JSON value into a simple CLR value where possible.
        /// </summary>
        private static object? ConvertJsonValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => value.GetRawText()
            };
        }

        // ---------------------------------------------------------------------
        // DICTIONARY HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Reads a dictionary string-like property.
        /// </summary>
        private static string? GetDictionaryString(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                string text => text,
                JsonElement json => json.ValueKind == JsonValueKind.String ? json.GetString() : json.GetRawText(),
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Reads a dictionary integer property with tolerant parsing.
        /// </summary>
        private static int GetDictionaryInt(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var value) || value is null)
            {
                return 0;
            }

            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var parsedInt) => parsedInt,
                _ when int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0
            };
        }

        /// <summary>
        /// Reads a dictionary long property with tolerant parsing.
        /// </summary>
        private static long GetDictionaryLong(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var value) || value is null)
            {
                return 0L;
            }

            return value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out var parsedLong) => parsedLong,
                _ when long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0L
            };
        }

        /// <summary>
        /// Reads a dictionary boolean property with tolerant parsing.
        /// </summary>
        private static bool GetDictionaryBool(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var value) || value is null)
            {
                return false;
            }

            return value switch
            {
                bool boolValue => boolValue,
                JsonElement json when json.ValueKind == JsonValueKind.True => true,
                JsonElement json when json.ValueKind == JsonValueKind.False => false,
                _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
                _ => false
            };
        }

        /// <summary>
        /// Reads a nullable dictionary double property with tolerant parsing.
        /// </summary>
        private static double? GetDictionaryNullableDouble(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                decimal decimalValue => (double)decimalValue,
                int intValue => intValue,
                long longValue => longValue,
                JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var parsedDouble) => parsedDouble,
                _ when double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        /// <summary>
        /// Reads a dictionary enum property with tolerant parsing.
        /// </summary>
        private static TEnum GetDictionaryEnum<TEnum>(
            IDictionary<string, object?> dict,
            string key)
            where TEnum : struct, Enum
        {
            var text = GetDictionaryString(dict, key);

            if (!string.IsNullOrWhiteSpace(text) &&
                Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return default;
        }

        /// <summary>
        /// Reads a metadata dictionary from a loosely typed dictionary property.
        /// </summary>
        private static IReadOnlyDictionary<string, object?> GetDictionaryMetadata(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetValue(dict, key, out var value) || value is null)
            {
                return new Dictionary<string, object?>();
            }

            if (value is IReadOnlyDictionary<string, object?> readOnlyDict)
            {
                return readOnlyDict;
            }

            if (value is IDictionary<string, object?> dictionary)
            {
                return new Dictionary<string, object?>(dictionary, StringComparer.Ordinal);
            }

            if (value is JsonElement json &&
                json.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);

                foreach (var member in json.EnumerateObject())
                {
                    result[member.Name] = ConvertJsonValue(member.Value);
                }

                return result;
            }

            return new Dictionary<string, object?>();
        }
    }
}