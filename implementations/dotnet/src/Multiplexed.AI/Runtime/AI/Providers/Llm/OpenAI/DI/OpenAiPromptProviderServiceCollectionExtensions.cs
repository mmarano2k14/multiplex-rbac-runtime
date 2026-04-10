using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Prompt.Options;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.AI.Providers.Llm.OpenAI.DI
{
    /// <summary>
    /// Provides DI registration helpers for the OpenAI prompt provider.
    /// </summary>
    public static class OpenAiPromptProviderServiceCollectionExtensions
    {
        /// <summary>
        /// Registers OpenAI provider configuration and SDK client.
        ///
        /// PURPOSE:
        /// - Supplies configuration to the OpenAI provider
        /// - Registers a reusable OpenAIClient (thread-safe)
        ///
        /// IMPORTANT:
        /// - The actual provider is discovered via assembly scanning
        /// - This method only provides configuration + SDK client
        /// </summary>
        public static IServiceCollection AddOpenAiPromptProvider(
            this IServiceCollection services,
            Action<OpenAiPromptProviderOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new OpenAiPromptProviderOptions();
            configure(options);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI configuration is invalid. ApiKey is required.");
            }

            // Register options
            services.AddSingleton(options);

            // Register OpenAIClient (thread-safe)
            services.AddSingleton(sp =>
            {
                var clientOptions = new OpenAIClientOptions();

                if (!string.IsNullOrWhiteSpace(options.Endpoint))
                {
                    clientOptions.Endpoint = new Uri(options.Endpoint, UriKind.Absolute);
                }

                if (!string.IsNullOrWhiteSpace(options.Organization))
                {
                    clientOptions.OrganizationId = options.Organization;
                }

                if (!string.IsNullOrWhiteSpace(options.Project))
                {
                    clientOptions.ProjectId = options.Project;
                }

                return new OpenAIClient(
                    new ApiKeyCredential(options.ApiKey),
                    clientOptions);
            });

            return services;
        }
    }
}
