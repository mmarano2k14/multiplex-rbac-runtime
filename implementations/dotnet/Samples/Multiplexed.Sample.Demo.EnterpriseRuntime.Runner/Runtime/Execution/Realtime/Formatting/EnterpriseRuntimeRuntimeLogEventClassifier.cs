using System.Text.Json;
using Multiplexed.Realtime.Events;
using Multiplexed.Realtime.Events.Runtime;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Realtime.Formatting
{
    /// <summary>
    /// Classifies raw runtime log events into readable enterprise runtime realtime events.
    /// </summary>
    public sealed class EnterpriseRuntimeRuntimeLogEventClassifier
    {
        /// <summary>
        /// Classifies a runtime log event.
        /// </summary>
        /// <param name="runtimeLogEvent">
        /// The runtime log event.
        /// </param>
        /// <returns>
        /// The readable realtime event.
        /// </returns>
        public EnterpriseRuntimeReadableRealtimeEvent Classify(
            RuntimeLogEvent runtimeLogEvent)
        {
            ArgumentNullException.ThrowIfNull(
                runtimeLogEvent);

            var category = runtimeLogEvent.Category ?? string.Empty;
            var message = runtimeLogEvent.Message ?? string.Empty;

            var kind = ResolveKind(
                category,
                message);

            return new EnterpriseRuntimeReadableRealtimeEvent
            {
                Kind = kind,
                Level = runtimeLogEvent.Level?.ToString() ?? "Unknown",
                Category = category,
                Message = message,
                OccurredAtUtc = runtimeLogEvent.OccurredAtUtc,
                ExecutionId = GetStringValue(runtimeLogEvent.Data, "ExecutionId") ?? ExtractQuotedValue(message, "ExecutionId="),
                StepName = GetStringValue(runtimeLogEvent.Data, "Step") ?? GetStringValue(runtimeLogEvent.Data, "StepName") ?? ExtractQuotedValue(message, "StepName=") ?? ExtractQuotedValue(message, "Step="),
                WorkerId = GetStringValue(runtimeLogEvent.Data, "WorkerId") ?? GetStringValue(runtimeLogEvent.Data, "Worker") ?? ExtractQuotedValue(message, "WorkerId=") ?? ExtractQuotedValue(message, "Worker="),
                ClaimToken = GetStringValue(runtimeLogEvent.Data, "ClaimToken") ?? ExtractQuotedValue(message, "ClaimToken="),
                Status = GetStringValue(runtimeLogEvent.Data, "Status") ?? ExtractQuotedValue(message, "Status="),
                Error = GetStringValue(runtimeLogEvent.Data, "Error") ?? ExtractQuotedValue(message, "Error="),
                SourceSignature = CreateSourceSignature(category, message),
                IsNoise = IsNoise(kind, category, message),
                IsImportant = IsImportant(kind)
            };
        }

        /// <summary>
        /// Resolves the readable event kind.
        /// </summary>
        /// <param name="category">
        /// The runtime event category.
        /// </param>
        /// <param name="message">
        /// The runtime event message.
        /// </param>
        /// <returns>
        /// The readable event kind.
        /// </returns>
        private static EnterpriseRuntimeRealtimeEventKind ResolveKind(
            string category,
            string message)
        {
            if (string.Equals(
                    category,
                    "ai.step.claimed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.StepClaimed;
            }

            if (string.Equals(
                    category,
                    "ai.execution.finalization.succeeded",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.FinalizationSucceeded;
            }

            if (string.Equals(
                    category,
                    "ai.execution.finalization.race.lost",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.FinalizationRaceLost;
            }

            if (string.Equals(
                    category,
                    "ai.snapshot.persisted",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.SnapshotPersisted;
            }

            if (string.Equals(
                    category,
                    "ai.execution.cleanup.skipped",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.CleanupSkipped;
            }

            if (string.Equals(
                    category,
                    "ai.execution.replay.restored",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.ReplayRestored;
            }

            if (message.Contains(
                    "Step completed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.StepCompleted;
            }

            if (message.Contains(
                    "Step failed",
                    StringComparison.OrdinalIgnoreCase) ||
                message.Contains(
                    "Step exception converted to failed result",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.StepFailed;
            }

            if (message.Contains(
                    "Step throttled",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.StepThrottled;
            }

            if (message.Contains(
                    "retry",
                    StringComparison.OrdinalIgnoreCase) ||
                message.Contains(
                    "recovered",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.RetryOrRecovery;
            }

            if (message.Contains(
                    "Finalization attempt",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.FinalizationAttempt;
            }

            if (message.Contains(
                    "worker idle delay",
                    StringComparison.OrdinalIgnoreCase))
            {
                return EnterpriseRuntimeRealtimeEventKind.WorkerIdle;
            }

            return EnterpriseRuntimeRealtimeEventKind.Diagnostic;
        }

        /// <summary>
        /// Determines whether the event is noisy for default verbose output.
        /// </summary>
        /// <param name="kind">
        /// The event kind.
        /// </param>
        /// <param name="category">
        /// The event category.
        /// </param>
        /// <param name="message">
        /// The event message.
        /// </param>
        /// <returns>
        /// True if the event is noisy; otherwise, false.
        /// </returns>
        private static bool IsNoise(
            EnterpriseRuntimeRealtimeEventKind kind,
            string category,
            string message)
        {
            if (kind == EnterpriseRuntimeRealtimeEventKind.StepThrottled)
            {
                return false;
            }

            if (kind is EnterpriseRuntimeRealtimeEventKind.WorkerIdle or EnterpriseRuntimeRealtimeEventKind.FinalizationAttempt)
            {
                return true;
            }

            if (string.Equals(
                    category,
                    "ai.execution.info",
                    StringComparison.OrdinalIgnoreCase) &&
                kind == EnterpriseRuntimeRealtimeEventKind.Diagnostic)
            {
                return true;
            }

            if (message.Contains(
                    "Concurrency lease released after failed claim",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (message.Contains(
                    "[AI DAG STORE] Specific step claimed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (message.Contains(
                    "[AI SNAPSHOT] Persisted terminal snapshot",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (message.Contains(
                    "[AI CLEANUP]",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the event is important.
        /// </summary>
        /// <param name="kind">
        /// The event kind.
        /// </param>
        /// <returns>
        /// True if the event is important; otherwise, false.
        /// </returns>
        private static bool IsImportant(
            EnterpriseRuntimeRealtimeEventKind kind)
        {
            return kind is EnterpriseRuntimeRealtimeEventKind.StepFailed
                or EnterpriseRuntimeRealtimeEventKind.StepThrottled
                or EnterpriseRuntimeRealtimeEventKind.FinalizationSucceeded
                or EnterpriseRuntimeRealtimeEventKind.FinalizationRaceLost
                or EnterpriseRuntimeRealtimeEventKind.SnapshotPersisted
                or EnterpriseRuntimeRealtimeEventKind.ReplayRestored;
        }

        /// <summary>
        /// Gets a string value from runtime log data.
        /// </summary>
        /// <param name="data">
        /// The runtime log data object.
        /// </param>
        /// <param name="key">
        /// The key to read.
        /// </param>
        /// <returns>
        /// The string value, or null if unavailable.
        /// </returns>
        private static string? GetStringValue(
            object? data,
            string key)
        {
            if (data is null)
            {
                return null;
            }

            if (data is IReadOnlyDictionary<string, object?> dictionary &&
                dictionary.TryGetValue(
                    key,
                    out var value))
            {
                return value?.ToString();
            }

            if (data is IDictionary<string, object?> mutableDictionary &&
                mutableDictionary.TryGetValue(
                    key,
                    out var mutableValue))
            {
                return mutableValue?.ToString();
            }

            if (data is JsonElement jsonElement &&
                jsonElement.ValueKind == JsonValueKind.Object &&
                jsonElement.TryGetProperty(
                    key,
                    out var property))
            {
                return property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : property.ToString();
            }

            var serialized = JsonSerializer.Serialize(
                data);

            using var document = JsonDocument.Parse(
                serialized);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(
                    key,
                    out var rootProperty))
            {
                return rootProperty.ValueKind == JsonValueKind.String
                    ? rootProperty.GetString()
                    : rootProperty.ToString();
            }

            return null;
        }

        /// <summary>
        /// Extracts a single-quoted value after a message prefix.
        /// </summary>
        /// <param name="message">
        /// The message to inspect.
        /// </param>
        /// <param name="prefix">
        /// The value prefix.
        /// </param>
        /// <returns>
        /// The extracted value, or null if not found.
        /// </returns>
        private static string? ExtractQuotedValue(
            string message,
            string prefix)
        {
            var prefixIndex = message.IndexOf(
                prefix,
                StringComparison.OrdinalIgnoreCase);

            if (prefixIndex < 0)
            {
                return null;
            }

            var start = prefixIndex + prefix.Length;

            if (start >= message.Length)
            {
                return null;
            }

            if (message[start] == '\'')
            {
                start++;
            }

            var end = message.IndexOf(
                '\'',
                start);

            if (end < 0)
            {
                end = message.IndexOf(
                    ',',
                    start);
            }

            if (end < 0)
            {
                end = message.IndexOf(
                    '.',
                    start);
            }

            if (end < 0)
            {
                end = message.Length;
            }

            return message[start..end].Trim();
        }

        /// <summary>
        /// Creates a lightweight source signature from category and message prefix.
        /// </summary>
        /// <param name="category">
        /// The runtime event category.
        /// </param>
        /// <param name="message">
        /// The runtime event message.
        /// </param>
        /// <returns>
        /// The source signature.
        /// </returns>
        private static string CreateSourceSignature(
            string category,
            string message)
        {
            var normalizedMessage = message.Length <= 80
                ? message
                : message[..80];

            return $"{category}:{normalizedMessage}";
        }
    }
}