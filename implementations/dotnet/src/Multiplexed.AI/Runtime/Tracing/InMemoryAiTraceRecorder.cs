using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.Abstractions.AI.Tracing;

namespace Multiplexed.AI.Runtime.Tracing
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiTraceRecorder"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Stores completed AI runtime trace records in memory.
    /// - Feeds the trace timeline from completed trace records.
    ///
    /// NAMING:
    /// - Category identifies the runtime component.
    /// - Name identifies the normalized operation result.
    ///
    /// EXAMPLES:
    /// - dag-store / claim.succeeded
    /// - dag-store / complete.succeeded
    /// - retention / retention.succeeded
    /// - step / execute.succeeded
    /// - execution / execution.succeeded
    ///
    /// IMPORTANT:
    /// - This implementation is process-local and non-durable.
    /// - It is suitable for tests, diagnostics, local runtime inspection, and early UI work.
    /// - Durable/exported tracing should use another recorder or exporter implementation later.
    /// </remarks>
    public sealed class InMemoryAiTraceRecorder : IAiTraceRecorder
    {
        private readonly ConcurrentQueue<AiTraceRecord> _records = new();
        private readonly IAiTraceTimeline _timeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryAiTraceRecorder"/> class.
        /// </summary>
        /// <param name="timeline">The timeline receiving projected trace events.</param>
        public InMemoryAiTraceRecorder(IAiTraceTimeline timeline)
        {
            _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        }

        /// <inheritdoc />
        public void Record(AiTraceRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            _records.Enqueue(record);

            if (string.IsNullOrWhiteSpace(record.ExecutionId))
            {
                return;
            }

            _timeline.Add(new AiTraceEvent
            {
                ExecutionId = record.ExecutionId,
                TimestampUtc = record.CompletedAtUtc ?? DateTime.UtcNow,
                Category = ResolveCategory(record),
                Name = ResolveEventName(record),
                StepId = record.StepId,
                Tags = BuildTimelineTags(record)
            });
        }

        /// <inheritdoc />
        public IReadOnlyList<AiTraceRecord> Snapshot()
        {
            return _records.ToArray().ToList();
        }

        private static string ResolveCategory(AiTraceRecord record)
        {
            if (record.Tags.TryGetValue("component", out var component) &&
                component is string componentName &&
                !string.IsNullOrWhiteSpace(componentName))
            {
                return NormalizeComponent(componentName);
            }

            return record.Operation switch
            {
                "execution" => "execution",
                "step" => "step",
                "storage" => "dag-store",
                "retention" => "retention",
                "resolver" => "resolver",
                _ => "runtime"
            };
        }

        private static string ResolveEventName(AiTraceRecord record)
        {
            var result = record.Failed
                ? "failed"
                : record.Succeeded
                    ? "succeeded"
                    : "completed";

            if (record.Tags.TryGetValue("operation", out var taggedOperation) &&
                taggedOperation is string operation &&
                !string.IsNullOrWhiteSpace(operation))
            {
                return $"{operation}.{result}";
            }

            return record.Operation switch
            {
                "step" => $"execute.{result}",
                "execution" => $"execution.{result}",
                "retention" => $"retention.{result}",
                "resolver" => $"resolve.{result}",
                _ => $"{record.Operation}.{result}"
            };
        }

        private static string ResolveOperationName(AiTraceRecord record)
        {
            if (record.Tags.TryGetValue("operation", out var taggedOperation) &&
                taggedOperation is string operation &&
                !string.IsNullOrWhiteSpace(operation))
            {
                return NormalizeOperation(operation);
            }

            return record.Operation switch
            {
                "step" => "execute",
                "execution" => "execution",
                "retention" => "retention",
                "resolver" => "resolve",
                "storage" => "operation",
                _ => NormalizeOperation(record.Operation)
            };
        }

        private static string NormalizeComponent(string component)
        {
            return component.Trim().ToLowerInvariant() switch
            {
                "storage" => "dag-store",
                "dagstore" => "dag-store",
                "dag-store" => "dag-store",
                "redis" => "dag-store",
                "redis-dag-store" => "dag-store",
                "execution" => "execution",
                "step" => "step",
                "retention" => "retention",
                "resolver" => "resolver",
                "payload" => "payload",
                _ => component.Trim().ToLowerInvariant()
            };
        }

        private static string NormalizeOperation(string operation)
        {
            return operation.Trim() switch
            {
                "TryClaimNextReadyStep" => "claim",
                "TryCompleteStep" => "complete",
                "TryFailStepException" => "fail.exception",
                "TryFailStepResult" => "fail.result",
                "RecoverTimedOutSteps" => "recover",
                "TryFinalizeExecution" => "finalize",
                "ApplyRetentionPersistAndWarm" => "retention",
                _ => operation.Trim()
                    .Replace(" ", "-", StringComparison.Ordinal)
                    .ToLowerInvariant()
            };
        }

        private static IDictionary<string, object?> BuildTimelineTags(AiTraceRecord record)
        {
            var tags = new Dictionary<string, object?>(record.Tags, StringComparer.OrdinalIgnoreCase)
            {
                ["traceId"] = record.Id,
                ["rawOperation"] = record.Operation,
                ["category"] = ResolveCategory(record),
                ["name"] = ResolveEventName(record),
                ["startedAtUtc"] = record.StartedAtUtc,
                ["completedAtUtc"] = record.CompletedAtUtc,
                ["durationMs"] = record.Duration?.TotalMilliseconds,
                ["succeeded"] = record.Succeeded,
                ["failed"] = record.Failed
            };

            if (!string.IsNullOrWhiteSpace(record.ErrorType))
            {
                tags["errorType"] = record.ErrorType;
            }

            if (!string.IsNullOrWhiteSpace(record.ErrorMessage))
            {
                tags["errorMessage"] = record.ErrorMessage;
            }

            if (record.Tags.TryGetValue("compactedCount", out var compact))
            {
                tags["compactedCount"] = compact;
            }

            if (record.Tags.TryGetValue("evictedCount", out var evict))
            {
                tags["evictedCount"] = evict;
            }

            if (record.Tags.TryGetValue("removedSteps", out var removed))
            {
                tags["removedSteps"] = removed;
            }

            if (record.Tags.TryGetValue("skipped", out var skipped))
            {
                tags["skipped"] = skipped;
            }

            return tags;
        }
    }
}