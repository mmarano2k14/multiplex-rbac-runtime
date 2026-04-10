using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Prompt;
using Multiplexed.Abstractions.AI.Prompt.Models;

namespace Multiplexed.AI.Runtime.AI.Prompt
{
    /// <summary>
    /// Default implementation of <see cref="IAiPromptExecutor"/>.
    ///
    /// This component orchestrates the full provider-agnostic prompt flow:
    /// 1. Validate the incoming request
    /// 2. Render the prompt template
    /// 3. Resolve the provider dynamically
    /// 4. Execute the provider call
    /// 5. Parse the returned raw text
    /// 6. Return a normalized, serializable prompt result
    ///
    /// This class does not contain provider SDK logic.
    /// It delegates transport concerns to the resolved provider.
    /// </summary>
    public sealed class DefaultAiPromptExecutor : IAiPromptExecutor
    {
        private readonly IAiPromptTemplateRenderer _templateRenderer;
        private readonly IAiPromptProviderResolver _providerResolver;
        private readonly IAiPromptResultParser _resultParser;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiPromptExecutor"/> class.
        /// </summary>
        public DefaultAiPromptExecutor(
            IAiPromptTemplateRenderer templateRenderer,
            IAiPromptProviderResolver providerResolver,
            IAiPromptResultParser resultParser)
        {
            _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));
            _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
            _resultParser = resultParser ?? throw new ArgumentNullException(nameof(resultParser));
        }

        /// <inheritdoc />
        public async Task<AiPromptResult> ExecuteAsync(
            AiPromptRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.ProviderKey))
            {
                throw new ArgumentException("ProviderKey is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException("Model is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.PromptTemplate))
            {
                throw new ArgumentException("PromptTemplate is required.", nameof(request));
            }

            var renderedPrompt = _templateRenderer.Render(
                request.PromptTemplate,
                request.Variables);

            var provider = _providerResolver.Resolve(request.ProviderKey);

            var providerRequest = new AiPromptProviderRequest
            {
                Model = request.Model,
                Prompt = renderedPrompt,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                ResponseFormat = request.ResponseFormat,
                Metadata = request.Metadata
            };

            var providerResponse = await provider.ExecuteAsync(
                providerRequest,
                cancellationToken);

            var parsedResult = _resultParser.Parse(
                providerResponse.RawText,
                request.ResponseFormat);

            return new AiPromptResult
            {
                ProviderKey = request.ProviderKey,
                Model = request.Model,
                RawText = providerResponse.RawText,
                ParsedResult = parsedResult,
                InputTokens = providerResponse.InputTokens,
                OutputTokens = providerResponse.OutputTokens,
                TotalTokens = providerResponse.TotalTokens,
                FinishReason = providerResponse.FinishReason,
                PromptVersion = request.PromptVersion,
                RenderedPromptHash = ComputeSha256(renderedPrompt),
                Metadata = providerResponse.ProviderMetadata
            };
        }

        /// <summary>
        /// Computes a stable SHA-256 hash for the specified string.
        /// </summary>
        private static string ComputeSha256(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var bytes = Encoding.UTF8.GetBytes(value);
            var hashBytes = SHA256.HashData(bytes);

            return Convert.ToHexString(hashBytes);
        }
    }
}