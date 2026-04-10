namespace Multiplexed.Abstractions.AI.Prompt
{
    /// <summary>
    /// Resolves a concrete prompt provider from a logical provider key.
    ///
    /// This abstraction allows pipelines to remain configuration-driven
    /// while keeping the runtime independent from provider-specific types.
    /// </summary>
    public interface IAiPromptProviderResolver
    {
        /// <summary>
        /// Resolves the provider associated with the specified provider key.
        /// </summary>
        /// <param name="providerKey">
        /// The logical provider key configured by the pipeline or runtime.
        /// </param>
        /// <returns>
        /// The matching prompt provider.
        /// </returns>
        IAiPromptProvider Resolve(string providerKey);
    }
}