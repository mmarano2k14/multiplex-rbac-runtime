using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Prompt;
using Multiplexed.Abstractions.AI.Prompt.Models;

namespace Multiplexed.AI.Runtime.AI.Providers.Llm.Fake
{
    /// <summary>
    /// Built-in fake AI prompt provider used for local development,
    /// deterministic runtime validation, and integration testing.
    ///
    /// PURPOSE:
    /// - Provides a provider implementation that requires no external SDK
    /// - Produces deterministic outputs for prompt pipeline verification
    /// - Helps validate provider discovery, resolution, and step execution
    ///
    /// DESIGN:
    /// - This provider is intentionally simple
    /// - It behaves like a normal AI prompt provider from the runtime point of view
    /// - It always returns a normalized response without external network calls
    ///
    /// IMPORTANT:
    /// - This provider is not intended for production AI inference
    /// - It exists to validate the orchestration pipeline safely and predictably
    /// </summary>
    [AiPromptProvider("fake")]
    public sealed class FakeAiPromptProvider : IAiPromptProvider
    {
        /// <summary>
        /// Gets the last request received by the provider.
        ///
        /// This is useful for debugging and deterministic assertions in tests.
        /// </summary>
        public AiPromptProviderRequest? LastRequest { get; private set; }

        /// <inheritdoc />
        public Task<AiPromptProviderResponse> ExecuteAsync(
            AiPromptProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            LastRequest = request;

            return Task.FromResult(new AiPromptProviderResponse
            {
                RawText = "FAKE_RESPONSE: " + request.Prompt,
                InputTokens = 10,
                OutputTokens = 20,
                TotalTokens = 30,
                FinishReason = "stop",
                ProviderMetadata = new Dictionary<string, object?>
                {
                    ["providerType"] = "fake",
                    ["deterministic"] = true
                }
            });
        }
    }
}