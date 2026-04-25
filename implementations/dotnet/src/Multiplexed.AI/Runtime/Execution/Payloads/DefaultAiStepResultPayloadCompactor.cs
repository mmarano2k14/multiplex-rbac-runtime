using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Centralized payload compaction for all step results.
    ///
    /// PURPOSE:
    /// - Ensures all execution paths (DAG, operations, RAG, etc.) use the same compaction logic.
    /// - Prevents bypass when steps do not go through AiStepExecutor.
    /// - Keeps Redis/Lua and snapshots small by externalizing large result values.
    ///
    /// IMPORTANT:
    /// - Externalized values are NOT removed without trace.
    /// - The result keeps a minimal summary in Value/Data so the state remains inspectable.
    /// - The full content is stored in Payload/DataPayloads and resolved through IAiExecutionPayloadResolver.
    /// </summary>
    public sealed class DefaultAiStepResultPayloadCompactor : IAiStepResultPayloadCompactor
    {
        private readonly IAiExecutionDataPolicy _dataPolicy;
        private readonly IAiPayloadMetrics _payloadMetrics;

        public DefaultAiStepResultPayloadCompactor(
            IAiExecutionDataPolicy dataPolicy,
            IAiPayloadMetrics payloadMetrics)
        {
            _dataPolicy = dataPolicy ?? throw new ArgumentNullException(nameof(dataPolicy));
            _payloadMetrics = payloadMetrics ?? throw new ArgumentNullException(nameof(payloadMetrics));
        }

        public async Task CompactAsync(
            AiStepResult result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(result);

            // -----------------------------------------------------
            // VALUE → Payload
            // -----------------------------------------------------
            if (result.Value is not null && result.Payload is null)
            {
                var payload = await _dataPolicy.StoreAsync(
                    result.Value,
                    cancellationToken);

                result.Payload = payload;
                RecordPayloadCompaction(payload);

                if (!payload.IsInline)
                {
                    result.Value = CreatePayloadSummary(payload);
                }
            }

            // -----------------------------------------------------
            // DATA → DataPayloads
            // -----------------------------------------------------
            if (result.Data is null || result.Data.Count == 0)
            {
                return;
            }

            foreach (var entry in result.Data.ToList())
            {
                if (entry.Value is null)
                {
                    continue;
                }

                var payload = await _dataPolicy.StoreAsync(
                    entry.Value,
                    cancellationToken);

                RecordPayloadCompaction(payload);

                if (payload.IsInline)
                {
                    continue;
                }

                result.DataPayloads ??= new Dictionary<string, AiStoredPayload>(StringComparer.Ordinal);
                result.DataPayloads[entry.Key] = payload;

                // Keep minimal state summary.
                result.Data[entry.Key] = CreatePayloadSummary(payload);
            }
        }

        /// <summary>
        /// Records payload compaction metrics according to the storage decision.
        ///
        /// PURPOSE:
        /// - Counts payloads kept inline inside the execution state.
        /// - Counts payloads externalized to durable payload storage.
        /// - Tracks byte distribution between inline state and external payload storage.
        ///
        /// IMPORTANT:
        /// - The compactor is the correct place for this metric because it owns the
        ///   state compaction decision and knows whether the runtime state will keep
        ///   the original value or replace it with an externalized payload summary.
        /// </summary>
        private void RecordPayloadCompaction(AiStoredPayload payload)
        {
            var sizeBytes = payload.SizeBytes ?? 0L;

            if (payload.IsInline)
            {
                _payloadMetrics.RecordInlinePayload(sizeBytes);
                return;
            }

            _payloadMetrics.RecordExternalizedPayload(sizeBytes);
        }

        /// <summary>
        /// Creates the compact inline summary used when a payload is externalized.
        /// </summary>
        private static Dictionary<string, object?> CreatePayloadSummary(
            AiStoredPayload payload)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["payloadExternalized"] = true,
                ["artifactId"] = payload.ArtifactId,
                ["contentHash"] = payload.ContentHash,
                ["sizeBytes"] = payload.SizeBytes,
                ["contentType"] = payload.ContentType
            };
        }
    }
}