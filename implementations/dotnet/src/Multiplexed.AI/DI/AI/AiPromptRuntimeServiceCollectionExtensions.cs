using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Prompt;
using Multiplexed.AI.Runtime.AI.Prompt;

namespace Multiplexed.AI.DI.AI
{
    /// <summary>
    /// Provides DI registration helpers for the provider-agnostic AI prompt runtime.
    /// </summary>
    public static class AiPromptRuntimeServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the core AI prompt runtime services.
        /// </summary>
        public static IServiceCollection AddAiPromptRuntime(
            this IServiceCollection services,
            params Assembly[] providerAssemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(providerAssemblies);

            // IMPORTANT:
            // Use scoped lifetimes to stay aligned with step resolution and provider resolution.
            services.AddScoped<IAiPromptTemplateRenderer, DefaultAiPromptTemplateRenderer>();
            services.AddScoped<IAiPromptResultParser, DefaultAiPromptResultParser>();
            services.AddScoped<IAiPromptExecutor, DefaultAiPromptExecutor>();

            services.AddAiPromptProvidersFromAssemblies(providerAssemblies);

            return services;
        }
    }
}