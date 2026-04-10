using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Prompt;
using Multiplexed.Abstractions.AI.Prompt.Models;
using OpenAI;
using OpenAI.Responses;

namespace Multiplexed.AI.Runtime.AI.Providers.Llm.OpenAI
{
    /// <summary>
    /// Concrete AI prompt provider backed by the official OpenAI .NET SDK.
    ///
    /// PURPOSE:
    /// - Executes provider-agnostic prompt requests against OpenAI
    /// - Converts runtime requests into OpenAI Responses API calls
    /// - Normalizes OpenAI responses back into provider-agnostic runtime models
    ///
    /// DESIGN:
    /// - Uses an injected <see cref="OpenAIClient"/> from DI
    /// - Retrieves a <see cref="ResponsesClient"/> from the shared OpenAI client
    /// - Returns only normalized runtime-friendly data
    /// - Never exposes SDK-specific types outside this provider
    ///
    /// IMPORTANT:
    /// - This provider is transport-focused only
    /// - Template rendering, parsing, retry policy, and orchestration live elsewhere
    /// - API-level payload errors are surfaced as exceptions so the runtime can
    ///   classify them correctly for retry or failure handling
    /// </summary>
    [AiPromptProvider("openai")]
    public sealed class OpenAiPromptProvider : IAiPromptProvider
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        private readonly ResponsesClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAiPromptProvider"/> class.
        /// </summary>
        /// <param name="client">
        /// The shared OpenAI SDK client resolved from dependency injection.
        /// </param>
        public OpenAiPromptProvider(OpenAIClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            _client = client.GetResponsesClient();
        }

        /// <inheritdoc />
        public async Task<AiPromptProviderResponse> ExecuteAsync(
            AiPromptProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException("Model is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(request));
            }

            var responseOptions = BuildResponseOptions(request);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DefaultTimeout);

            ResponseResult response = await _client.CreateResponseAsync(
                responseOptions,
                timeoutCts.Token);

            //
            // The SDK can expose an API-level error on the payload itself.
            // Surface it explicitly so the runtime can classify it consistently.
            //
            if (response.Error is not null)
            {
                throw new InvalidOperationException(
                    $"OpenAI error: {response.Error.Message}");
            }

            var rawText = response.GetOutputText() ?? string.Empty;

            return new AiPromptProviderResponse
            {
                RawText = rawText,
                InputTokens = SafeToInt(response.Usage?.InputTokenCount),
                OutputTokens = SafeToInt(response.Usage?.OutputTokenCount),
                TotalTokens = SafeToInt(response.Usage?.TotalTokenCount),
                FinishReason = response.Status?.ToString(),
                ProviderMetadata = BuildProviderMetadata(response, request)
            };
        }

        /// <summary>
        /// Builds the OpenAI Responses API request from the provider-agnostic request.
        /// </summary>
        /// <param name="request">
        /// The normalized provider request.
        /// </param>
        /// <returns>
        /// A configured <see cref="CreateResponseOptions"/> instance.
        /// </returns>
        private static CreateResponseOptions BuildResponseOptions(AiPromptProviderRequest request)
        {
            var options = new CreateResponseOptions
            {
                Model = request.Model
            };

            //
            // Simple v1 JSON enforcement:
            // use an explicit instruction rather than advanced structured output features.
            //
            var prompt = string.Equals(
                request.ResponseFormat,
                "json",
                StringComparison.OrdinalIgnoreCase)
                ? "Return ONLY valid JSON.\n\n" + request.Prompt
                : request.Prompt;

            options.InputItems.Add(ResponseItem.CreateUserMessageItem(prompt));

            return options;
        }

        /// <summary>
        /// Builds normalized provider metadata in a runtime-safe dictionary form.
        /// </summary>
        /// <param name="response">
        /// The raw OpenAI response.
        /// </param>
        /// <param name="request">
        /// The normalized provider request.
        /// </param>
        /// <returns>
        /// A serializable metadata dictionary.
        /// </returns>
        private static Dictionary<string, object?> BuildProviderMetadata(
            ResponseResult response,
            AiPromptProviderRequest request)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["provider"] = "openai",
                ["model"] = request.Model,
                ["responseId"] = response.Id,
                ["status"] = response.Status?.ToString(),
                ["hasError"] = response.Error is not null
            };
        }

        /// <summary>
        /// Safely converts a nullable long token count to a nullable int.
        /// </summary>
        /// <param name="value">
        /// The nullable long value to convert.
        /// </param>
        /// <returns>
        /// A nullable int representation.
        /// </returns>
        private static int? SafeToInt(long? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            if (value.Value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value.Value < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)value.Value;
        }
    }
}