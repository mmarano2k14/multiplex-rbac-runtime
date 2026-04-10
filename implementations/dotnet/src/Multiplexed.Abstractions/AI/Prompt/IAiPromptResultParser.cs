namespace Multiplexed.Abstractions.AI.Prompt
{
    /// <summary>
    /// Parses raw LLM output into a normalized runtime-friendly object.
    ///
    /// This contract is especially useful when the pipeline expects
    /// structured output such as JSON.
    /// </summary>
    public interface IAiPromptResultParser
    {
        /// <summary>
        /// Parses raw text into a structured result when applicable.
        /// </summary>
        /// <param name="rawText">
        /// The raw text returned by the provider.
        /// </param>
        /// <param name="responseFormat">
        /// The expected response format, such as "json".
        /// </param>
        /// <returns>
        /// The parsed object, or null if parsing is not applicable.
        /// </returns>
        object? Parse(string rawText, string? responseFormat = null);
    }
}