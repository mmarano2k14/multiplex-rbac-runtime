namespace Multiplexed.Abstractions.AI.Execution.Payloads
{
    /// <summary>
    /// Represents a payload stored by the AI execution runtime.
    ///
    /// PURPOSE:
    /// - Provides a stable abstraction for execution data that may either be stored
    ///   inline in the execution state or externally in an artifact store.
    /// - Allows the runtime to progressively compact the execution ledger without
    ///   breaking existing execution, replay, snapshot, or binding behavior.
    ///
    /// DESIGN:
    /// - Small payloads can remain inline for fast access and simple binding.
    /// - Large payloads can later be moved out of the execution state and replaced
    ///   by an artifact reference.
    /// - This type is intentionally storage-oriented and does not define memory
    ///   semantics such as working memory or consolidated memory.
    ///
    /// IMPORTANT:
    /// - Inline payloads are still supported for backward compatibility.
    /// - Artifact-backed payloads must remain resolvable for replay when they are
    ///   required by deterministic execution.
    /// </summary>
    public sealed class AiStoredPayload
    {
        /// <summary>
        /// Gets whether the payload is stored inline.
        /// </summary>
        public bool IsInline { get; init; }

        /// <summary>
        /// Gets the inline value when the payload is stored directly in the
        /// execution state or step result.
        /// </summary>
        public object? InlineValue { get; init; }

        /// <summary>
        /// Gets the artifact identifier when the payload is stored externally.
        /// </summary>
        public string? ArtifactId { get; init; }

        /// <summary>
        /// Gets the stable content hash, when available.
        /// </summary>
        public string? ContentHash { get; init; }

        /// <summary>
        /// Gets the estimated payload size in bytes, when known.
        /// </summary>
        public long? SizeBytes { get; init; }

        /// <summary>
        /// Gets the payload content type, when known.
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// Creates an inline stored payload.
        /// </summary>
        public static AiStoredPayload Inline(object? value, long? sizeBytes = null, string? contentType = null)
        {
            return new AiStoredPayload
            {
                IsInline = true,
                InlineValue = value,
                SizeBytes = sizeBytes,
                ContentType = contentType
            };
        }

        /// <summary>
        /// Creates an artifact-backed stored payload.
        /// </summary>
        public static AiStoredPayload Artifact(
            string artifactId,
            string? contentHash = null,
            long? sizeBytes = null,
            string? contentType = null)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                throw new ArgumentException("Artifact id cannot be null or empty.", nameof(artifactId));
            }

            return new AiStoredPayload
            {
                IsInline = false,
                ArtifactId = artifactId,
                ContentHash = contentHash,
                SizeBytes = sizeBytes,
                ContentType = contentType
            };
        }
    }
}