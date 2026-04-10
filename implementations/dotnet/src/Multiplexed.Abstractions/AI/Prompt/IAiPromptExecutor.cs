using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Prompt.Models;

namespace Multiplexed.Abstractions.AI.Prompt
{
    /// <summary>
    /// Defines the high-level orchestration contract for prompt execution.
    ///
    /// Responsibilities:
    /// - Render a prompt template using runtime variables
    /// - Resolve the target provider dynamically
    /// - Execute the rendered prompt against the provider
    /// - Parse and normalize the returned response
    ///
    /// Important:
    /// This contract must remain fully provider-agnostic.
    /// It must never expose OpenAI, Anthropic, Ollama, Azure OpenAI,
    /// or any other SDK-specific types.
    /// </summary>
    public interface IAiPromptExecutor
    {
        /// <summary>
        /// Executes a prompt request and returns a normalized prompt result.
        /// </summary>
        /// <param name="request">
        /// The high-level prompt execution request.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the prompt execution.
        /// </param>
        /// <returns>
        /// A normalized, serializable, replay-safe prompt result.
        /// </returns>
        Task<AiPromptResult> ExecuteAsync(
            AiPromptRequest request,
            CancellationToken cancellationToken = default);
    }
}