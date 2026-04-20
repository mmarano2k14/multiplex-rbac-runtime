namespace Multiplexed.Abstractions.AI.Rag.Runtime
{
    /// <summary>
    /// Normalizes RAG step outputs before they are persisted or exposed as step results.
    ///
    /// PURPOSE:
    /// - Prevent structured models from degrading into weak JSON blobs
    /// - Preserve strong CLR shapes whenever possible
    /// - Keep step output replay-friendly and deterministic
    /// </summary>
    public interface IRagStepResultNormalizer
    {
        /// <summary>
        /// Normalizes a step result object.
        /// </summary>
        /// <param name="value">
        /// Raw result value.
        /// </param>
        /// <returns>
        /// Normalized result value.
        /// </returns>
        object? Normalize(object? value);
    }
}