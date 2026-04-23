namespace Multiplexed.Abstractions.AI.Execution.Payloads
{
    /// <summary>
    /// Resolves stored execution payloads into materialized runtime values.
    ///
    /// PURPOSE:
    /// - Hides whether a payload is stored inline or externally.
    /// - Keeps binding, replay, prompt, and RAG code from depending directly on the
    ///   physical storage strategy.
    ///
    /// DESIGN:
    /// - Inline payloads should be returned directly.
    /// - Artifact-backed payloads should be loaded through the configured artifact
    ///   storage mechanism.
    ///
    /// IMPORTANT:
    /// - This interface is introduced before externalization is enabled.
    /// - The first implementation can safely return inline values only.
    /// </summary>
    public interface IAiExecutionPayloadResolver
    {
        /// <summary>
        /// Resolves the stored payload into its runtime value.
        /// </summary>
        Task<object?> ResolveAsync(
            AiStoredPayload payload,
            CancellationToken cancellationToken = default);
    }
}