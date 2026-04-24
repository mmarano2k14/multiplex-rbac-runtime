using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Rag.Enums;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Enums;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Provides shared helpers for RAG step implementations.
    ///
    /// PURPOSE:
    /// - Centralizes common config extraction and result shaping.
    /// - Keeps individual RAG steps small, consistent, and runtime-safe.
    /// - Provides robust conversion helpers for persisted/distributed step results.
    ///
    /// IMPORTANT:
    /// - In a distributed runtime, step outputs may not remain strongly typed.
    /// - Values loaded from Redis / Mongo / JSON may reappear as:
    ///   - strongly typed CLR objects
    ///   - dictionaries
    ///   - <see cref="JsonElement"/>
    /// - This helper must therefore be resilient to runtime serialization boundaries.
    /// </summary>
    public static class RagStepHelper
    {
        /// <summary>
        /// Builds a generic <see cref="RagExecutionContext"/> from the current step execution context.
        ///
        /// PURPOSE:
        /// - Creates a normalized RAG execution envelope from the current DAG step.
        /// - Preserves query, correlation, declared inputs, and execution metadata.
        ///
        /// IMPORTANT:
        /// - This helper is intended for provider-oriented steps such as:
        ///   - rag.vector
        ///   - rag.sql
        ///   - rag.runtime
        /// - It should NOT be used by strongly typed plugin-based retrieval execution
        ///   where the real runtime execution context must be preserved.
        /// </summary>
        public static RagExecutionContext BuildRagExecutionContext(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var query = ResolveQuery(context);

            return new RagExecutionContext
            {
                QueryText = query,
                QueryKey = context.StepKey,
                CorrelationId = context.ExecutionId,
                Inputs = context.ResolveDeclaredInputs(includeReservedVariables: true),
                Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ExecutionId"] = context.ExecutionId,
                    ["StepName"] = context.StepName,
                    ["StepKey"] = context.StepKey
                }
            };
        }

        /// <summary>
        /// Reads the required provider key from the current step configuration.
        /// </summary>
        public static string GetRequiredProviderKey(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetStepConfigValue<string>("provider", out var provider) ||
                string.IsNullOrWhiteSpace(provider))
            {
                throw new InvalidOperationException(
                    "rag.retrieval: Missing required config 'provider'.");
            }

            return provider;
        }

        /// <summary>
        /// Reads the required operation key from the current step configuration.
        ///
        /// PURPOSE:
        /// - Used by generic retrieval steps such as rag.retrieval
        /// - Keeps config extraction logic consistent with other helper methods
        /// </summary>
        public static string GetRequiredOperation(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetStepConfigValue<string>("operation", out var operation) ||
                string.IsNullOrWhiteSpace(operation))
            {
                throw new InvalidOperationException(
                    "rag.retrieval: Missing required config 'operation'.");
            }

            return operation;
        }

        /// <summary>
        /// Resolves all declared step inputs, including reserved runtime variables.
        ///
        /// PURPOSE:
        /// - Provides a single consistent entry point for steps that need
        ///   all resolved runtime inputs.
        /// - Keeps retrieval/composition steps small and consistent.
        /// </summary>
        public static IReadOnlyDictionary<string, object?> GetResolvedInputs(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.ResolveDeclaredInputs(includeReservedVariables: true);
        }

        /// <summary>
        /// Resolves the full argument bag that should be passed to a RAG operation.
        ///
        /// PURPOSE:
        /// - Merges declared step inputs with selected resolved step configuration values.
        /// - Ensures generic retrieval operations can access argument-like config values
        ///   such as providerKey and executionMode when they are configured on the step.
        ///
        /// DESIGN:
        /// - Declared inputs always win over config values when the same key exists.
        /// - Structural config keys such as 'operation' are intentionally excluded.
        /// - Only argument-like config fields are merged to avoid surprising behavior.
        ///
        /// IMPORTANT:
        /// - This method is additive and does not change the behavior of
        ///   <see cref="GetResolvedInputs(AiStepExecutionContext)"/>.
        /// - Existing callers of <see cref="GetResolvedInputs(AiStepExecutionContext)"/>
        ///   remain unaffected.
        /// </summary>
        public static IReadOnlyDictionary<string, object?> GetResolvedOperationArguments(
            AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var merged = new Dictionary<string, object?>(
                context.ResolveDeclaredInputs(includeReservedVariables: true),
                StringComparer.Ordinal);

            AddResolvedStepConfigValueIfPresent(context, merged, "provider");
            AddResolvedStepConfigValueIfPresent(context, merged, "providerKey");
            AddResolvedStepConfigValueIfPresent(context, merged, "executionMode");
            AddResolvedStepConfigValueIfPresent(context, merged, "query");
            AddResolvedStepConfigValueIfPresent(context, merged, "sourceStep");
            AddResolvedStepConfigValueIfPresent(context, merged, "composer");

            return merged;
        }

        /// <summary>
        /// Gets the required RBAC execution context snapshot captured at execution creation time.
        ///
        /// IMPORTANT:
        /// - This is the persisted RBAC/runtime snapshot stored on the execution record.
        /// - This is NOT the live RBAC context.
        /// - This is NOT the AI execution context object used during orchestration.
        /// - This helper is intended for plugin-based flows that need access to
        ///   the persisted RBAC execution snapshot.
        /// </summary>
        public static ExecutionContextSnapshot GetRequiredExecutionContextSnapshot(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var snapshot = context.Execution.Record.ExecutionContextSnapshot;
            if (snapshot is null)
            {
                throw new InvalidOperationException(
                    "rag: The current execution does not contain an RBAC execution context snapshot.");
            }

            return snapshot;
        }

        /// <summary>
        /// Gets the RBAC execution context snapshot captured at execution creation time, if available.
        ///
        /// IMPORTANT:
        /// - This returns the persisted snapshot from the execution record.
        /// - This may be null depending on the execution lifecycle or test setup.
        /// </summary>
        public static ExecutionContextSnapshot? GetExecutionContextSnapshot(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.Execution.Record.ExecutionContextSnapshot;
        }

        /// <summary>
        /// Reads the required list of source step names from the current step configuration.
        /// </summary>
        public static IReadOnlyList<string> GetRequiredSourceSteps(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetStepConfigValue<object>("sourceSteps", out var raw) || raw is null)
            {
                throw new InvalidOperationException(
                    "rag.merge: Missing required config 'sourceSteps'.");
            }

            if (raw is IEnumerable<object> objectItems)
            {
                var values = objectItems
                    .Select(x => x?.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();

                if (values.Length == 0)
                {
                    throw new InvalidOperationException(
                        "rag.merge: Config 'sourceSteps' cannot be empty.");
                }

                return values;
            }

            if (raw is JsonElement json &&
                json.ValueKind == JsonValueKind.Array)
            {
                var values = json.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();

                if (values.Length == 0)
                {
                    throw new InvalidOperationException(
                        "rag.merge: Config 'sourceSteps' cannot be empty.");
                }

                return values;
            }

            throw new InvalidOperationException(
                "rag.merge: Config 'sourceSteps' must be an array.");
        }

        /// <summary>
        /// Resolves and converts a required retrieval batch from a previous step.
        ///
        /// PURPOSE:
        /// - Centralizes cross-step retrieval batch resolution.
        /// - Supports both strongly typed and deserialized runtime representations.
        ///
        /// SUPPORTED INPUT SHAPES:
        /// - <see cref="RagRetrievalBatch"/>
        /// - <see cref="IDictionary{TKey,TValue}"/> representing a serialized batch
        /// - <see cref="JsonElement"/> representing a serialized batch
        ///
        /// IMPORTANT:
        /// - This method fails fast if the batch is missing or cannot be converted.
        /// - This is intentional because downstream compose / merge logic depends on
        ///   a valid retrieval batch.
        /// </summary>
        public static RagRetrievalBatch GetRequiredBatch(
            AiStepExecutionContext context,
            string stepName)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            if (!context.TryResolvePath($"steps.{stepName}.result.data.batch", out object? raw) ||
                raw is null)
            {
                throw new InvalidOperationException(
                    $"rag: Unable to resolve 'result.data.batch' from step '{stepName}'.");
            }

            if (TryConvertToRetrievalBatch(raw, out var batch))
            {
                return batch;
            }

            throw new InvalidOperationException(
                $"rag: Retrieval batch from step '{stepName}' could not be converted from type '{raw.GetType().FullName}'.");
        }

        /// <summary>
        /// Resolves and converts a required retrieval batch from a previous step,
        /// including support for externalized payload-backed result data.
        ///
        /// PURPOSE:
        /// - Supports the new payload compaction model.
        /// - Reads inline data first for backward compatibility.
        /// - Falls back to Result.DataPayloads["batch"] when the batch was externalized.
        /// - Rehydrates the payload through <see cref="IAiExecutionPayloadResolver"/>.
        ///
        /// IMPORTANT:
        /// - This method is required when upstream step results may have been compacted
        ///   before being persisted to Redis/Lua or Mongo snapshots.
        /// - The synchronous <see cref="GetRequiredBatch"/> method is kept for legacy callers
        ///   that still operate on inline-only results.
        /// </summary>
        public static async Task<RagRetrievalBatch> GetRequiredBatchAsync(
            AiStepExecutionContext context,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            // -------------------------------------------------------------
            // 1. Backward-compatible inline path.
            //
            // Before payload compaction:
            // steps.{step}.result.data.batch contains the full RagRetrievalBatch.
            //
            // After payload compaction:
            // steps.{step}.result.data.batch contains only a compact payload summary.
            // -------------------------------------------------------------
            if (context.TryResolvePath($"steps.{stepName}.result.data.batch", out object? raw) &&
                raw is not null &&
                TryConvertToRetrievalBatch(raw, out var inlineBatch))
            {
                return inlineBatch;
            }

            // -------------------------------------------------------------
            // 2. Payload-backed path.
            //
            // After compaction:
            // steps.{step}.result.data.batch      = summary
            // steps.{step}.result.dataPayloads[]  = artifact reference
            // -------------------------------------------------------------
            if (!context.State.Steps.TryGetValue(stepName, out var stepState) ||
                stepState.Result is null)
            {
                throw new InvalidOperationException(
                    $"rag: Unable to resolve result from step '{stepName}'.");
            }

            var result = stepState.Result;

            if (result.DataPayloads is null ||
                !result.DataPayloads.TryGetValue("batch", out var payload) ||
                payload is null)
            {
                throw new InvalidOperationException(
                    $"rag: Unable to resolve 'result.data.batch' from step '{stepName}'. " +
                    "The value was not available inline and no payload reference was found.");
            }

            var payloadResolver = context.Execution.Services
                .GetRequiredService<IAiExecutionPayloadResolver>();

            var resolved = await payloadResolver.ResolveAsync(
                payload,
                cancellationToken).ConfigureAwait(false);

            if (resolved is not null &&
                TryConvertToRetrievalBatch(resolved, out var payloadBatch))
            {
                return payloadBatch;
            }

            throw new InvalidOperationException(
                $"rag: Retrieval batch from step '{stepName}' could not be converted from payload-backed value type '{resolved?.GetType().FullName ?? "null"}'.");
        }

        /// <summary>
        /// Builds a consistent serializable result data bag for retrieval steps.
        /// </summary>
        public static Dictionary<string, object?> BuildRetrievalStepResultData(
            RagRetrievalBatch batch,
            string providerOrOperationKey)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentException.ThrowIfNullOrWhiteSpace(providerOrOperationKey);

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["providerKey"] = providerOrOperationKey,
                ["itemCount"] = batch.Items.Count,
                ["batch"] = batch,
                ["diagnostics"] = batch.Diagnostics
            };
        }

        /// <summary>
        /// Builds a consistent serializable result data bag for composition steps.
        /// </summary>
        public static Dictionary<string, object?> BuildCompositionStepResultData(
            RagComposedContext<RagStructuredContext> composed)
        {
            ArgumentNullException.ThrowIfNull(composed);

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["context"] = composed.Context,
                ["fragments"] = composed.Fragments,
                ["fragmentCount"] = composed.Fragments.Count,
                ["metadata"] = composed.Metadata
            };
        }

        /// <summary>
        /// Resolves the retrieval query from step config when available, otherwise falls back
        /// to a declared input named 'query', and finally to an empty string.
        /// </summary>
        private static string ResolveQuery(AiStepExecutionContext context)
        {
            if (context.TryGetStepConfigValue<string>("query", out var configQuery) &&
                !string.IsNullOrWhiteSpace(configQuery))
            {
                return configQuery;
            }

            var inputs = context.ResolveDeclaredInputs(includeReservedVariables: true);

            if (inputs.TryGetValue("query", out var inputQuery) && inputQuery is not null)
            {
                return inputQuery.ToString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Adds a resolved config value to the merged operation argument bag when present.
        ///
        /// RULES:
        /// - Existing input values are never overwritten.
        /// - Missing config keys are ignored.
        /// - Config references such as 'state.providerKey' are resolved through the step context.
        /// </summary>
        private static void AddResolvedStepConfigValueIfPresent(
            AiStepExecutionContext context,
            IDictionary<string, object?> target,
            string key)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (target.ContainsKey(key))
            {
                return;
            }

            if (!context.TryGetStepConfigValue<object>(key, out var rawValue))
            {
                return;
            }

            target[key] = ResolveConfigValue(context, rawValue);
        }

        /// <summary>
        /// Resolves a config value when it is expressed as a path-like string such as
        /// 'state.providerKey' or 'steps.someStep.result...'.
        ///
        /// PURPOSE:
        /// - Allows operation arguments to come from step config while still supporting
        ///   runtime path resolution.
        ///
        /// IMPORTANT:
        /// - If the value is a JSON string, it is treated the same way as a normal string
        ///   and is allowed to flow through path resolution first.
        /// - If the value does not look like a resolvable path, it is returned as-is.
        /// - If path resolution fails, the original raw value is returned to preserve
        ///   backward compatibility and avoid unexpected runtime failures.
        /// </summary>
        private static object? ResolveConfigValue(
            AiStepExecutionContext context,
            object? rawValue)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (rawValue is null)
            {
                return null;
            }

            if (rawValue is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.String)
                {
                    return ResolveStringConfigValue(context, json.GetString(), rawValue);
                }

                return ConvertJsonValue(json);
            }

            if (rawValue is string text)
            {
                return ResolveStringConfigValue(context, text, rawValue);
            }

            return rawValue;
        }

        /// <summary>
        /// Resolves one string config value, attempting runtime path resolution first
        /// and falling back to the original value when no path match is available.
        /// </summary>
        private static object? ResolveStringConfigValue(
            AiStepExecutionContext context,
            string? text,
            object? rawValue)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (!LooksLikeResolvablePath(text))
            {
                return text;
            }

            if (context.TryResolvePath<object?>(text, out var resolved))
            {
                return resolved;
            }

            return rawValue is JsonElement json && json.ValueKind == JsonValueKind.String
                ? json.GetString()
                : rawValue;
        }

        /// <summary>
        /// Determines whether the supplied text looks like a runtime-resolvable path.
        /// </summary>
        private static bool LooksLikeResolvablePath(string text)
        {
            return text.StartsWith("state.", StringComparison.Ordinal) ||
                   text.StartsWith("steps.", StringComparison.Ordinal) ||
                   text.StartsWith("execution.", StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------
        // CONVERSION HELPERS
        // ---------------------------------------------------------------------

        private static bool TryConvertToRetrievalBatch(
            object raw,
            out RagRetrievalBatch batch)
        {
            ArgumentNullException.ThrowIfNull(raw);

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

        private static bool TryConvertDictionaryToRetrievalBatch(
            IDictionary<string, object?> dict,
            out RagRetrievalBatch batch)
        {
            try
            {
                var items = new List<RagNormalizedItem>();

                if (TryGetDictionaryValue(dict, "Items", out var rawItems) &&
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

                if (TryGetDictionaryValue(dict, "Diagnostics", out var rawDiagnostics) &&
                    rawDiagnostics is not null)
                {
                    diagnostics = TryConvertDiagnostics(rawDiagnostics, out var convertedDiagnostics)
                        ? convertedDiagnostics
                        : null;
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

        private static RagRetrievalDiagnostics ConvertDictionaryToDiagnostics(
            IDictionary<string, object?> dict)
        {
            var providers = new List<RagProviderExecutionDiagnostics>();

            if (TryGetDictionaryValue(dict, "Providers", out var rawProviders) &&
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

        private static bool TryGetDictionaryValue(
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

        private static string? GetDictionaryString(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetDictionaryValue(dict, key, out var value) || value is null)
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

        private static int GetDictionaryInt(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetDictionaryValue(dict, key, out var value) || value is null)
            {
                return 0;
            }

            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                JsonElement json => json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var parsedInt)
                    ? parsedInt
                    : 0,
                _ when int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0
            };
        }

        private static long GetDictionaryLong(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetDictionaryValue(dict, key, out var value) || value is null)
            {
                return 0L;
            }

            return value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                JsonElement json => json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out var parsedLong)
                    ? parsedLong
                    : 0L,
                _ when long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0L
            };
        }

        private static double? GetDictionaryNullableDouble(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetDictionaryValue(dict, key, out var value) || value is null)
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
                JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var parsedDouble)
                    => parsedDouble,
                _ when double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    => parsed,
                _ => null
            };
        }

        private static bool GetDictionaryBool(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetDictionaryValue(dict, key, out var value) || value is null)
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

        private static IReadOnlyDictionary<string, object?> GetDictionaryMetadata(
            IDictionary<string, object?> dict,
            string key)
        {
            if (!TryGetDictionaryValue(dict, key, out var value) || value is null)
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