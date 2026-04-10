using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Prompt.Models;

namespace Multiplexed.Abstractions.AI.Prompt
{
    /// <summary>
    /// Defines the low-level contract implemented by a concrete LLM provider.
    ///
    /// A provider is responsible only for sending a rendered prompt to a
    /// specific backend and returning a normalized provider response.
    ///
    /// Provider identity is declared through <see cref="AiPromptProviderAttribute"/>
    /// and resolved by the provider registry.
    /// </summary>
    public interface IAiPromptProvider
    {
        /// <summary>
        /// Executes a rendered prompt request against the provider.
        /// </summary>
        /// <param name="request">
        /// The provider-level prompt request containing the final rendered prompt.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the provider call.
        /// </param>
        /// <returns>
        /// A normalized provider response containing raw text and metadata.
        /// </returns>
        Task<AiPromptProviderResponse> ExecuteAsync(
            AiPromptProviderRequest request,
            CancellationToken cancellationToken = default);
    }
}