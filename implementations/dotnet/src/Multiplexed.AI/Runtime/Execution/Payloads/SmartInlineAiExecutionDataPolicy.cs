using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution.Payloads;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Smart execution data policy with conditional payload externalization.
    ///
    /// PURPOSE:
    /// - Determines whether execution data should be stored inline or externally
    /// - Enables ledger compaction by offloading large payloads
    ///
    /// DESIGN:
    /// - Small payloads remain inline (backward compatible)
    /// - Large payloads are serialized and stored in <see cref="IAiPayloadStore"/>
    /// - The returned <see cref="AiStoredPayload"/> contains either:
    ///     - Inline value
    ///     - Artifact reference
    ///
    /// IMPORTANT:
    /// - This policy does NOT remove inline values from AiStepResult
    /// - This preserves replay, binding, and existing tests
    /// - Resolver is responsible for loading artifacts
    ///
    /// SAFETY:
    /// - Fully backward compatible
    /// - No change in runtime semantics
    /// - Externalization is transparent to consumers
    /// </summary>
    public sealed class SmartInlineAiExecutionDataPolicy : IAiExecutionDataPolicy
    {
        /// <summary>
        /// Maximum size (in bytes) before switching to artifact storage.
        ///
        /// NOTE:
        /// - This is based on JSON serialized length
        /// - Can be tuned based on Redis limits / performance targets
        /// </summary>
        private const int MaxInlineSizeBytes = 2048;

        private readonly IAiPayloadStore _store;

        public SmartInlineAiExecutionDataPolicy(IAiPayloadStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Stores a value as either inline payload or external artifact.
        ///
        /// BEHAVIOR:
        /// - Null values remain inline
        /// - Small values remain inline
        /// - Large values are serialized and stored externally
        ///
        /// IMPORTANT:
        /// - Serialization must be deterministic
        /// - This method must never throw for normal payloads
        /// </summary>
        public async Task<AiStoredPayload> StoreAsync(
            object? value,
            CancellationToken cancellationToken = default)
        {
            if (value is null)
            {
                return AiStoredPayload.Inline(null);
            }

            try
            {
                // Serialize to JSON for size estimation and artifact storage
                var json = JsonSerializer.Serialize(value);
                var size = json.Length;

                // ---------------------------------------------------------
                // INLINE MODE
                // ---------------------------------------------------------
                if (size <= MaxInlineSizeBytes)
                {
                    return AiStoredPayload.Inline(value);
                }

                // ---------------------------------------------------------
                // ARTIFACT MODE (NEW 🔥)
                // ---------------------------------------------------------
                var key = await _store.SaveAsync(json, cancellationToken);

                return AiStoredPayload.Artifact(key);
            }
            catch
            {
                // SAFETY FALLBACK:
                // If serialization fails, keep inline to avoid breaking execution
                return AiStoredPayload.Inline(value);
            }
        }
    }
}