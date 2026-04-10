using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Prompt;
using Multiplexed.AI.Runtime.AI.Providers.Llm;

namespace Multiplexed.AI.DI.AI
{
    /// <summary>
    /// Provides DI registration helpers for AI prompt providers discovered by assembly scanning.
    ///
    /// DESIGN:
    /// - Mirrors the same registration strategy used for AI steps
    /// - Registers each discovered concrete provider type directly in DI
    /// - Registers the provider resolver using the same assembly list
    ///
    /// IMPORTANT:
    /// - Providers are resolved by concrete type, not by interface enumeration
    /// - The resolver is responsible for mapping provider keys to concrete provider types
    /// </summary>
    public static class AiPromptProviderServiceCollectionExtensions
    {
        /// <summary>
        /// Discovers and registers AI prompt providers from the specified assemblies.
        /// </summary>
        /// <param name="services">
        /// The service collection to update.
        /// </param>
        /// <param name="assemblies">
        /// The assemblies to scan for AI prompt providers.
        /// </param>
        /// <returns>
        /// The same service collection for fluent chaining.
        /// </returns>
        public static IServiceCollection AddAiPromptProvidersFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assemblies);

            var providerTypes = assemblies
                .Distinct()
                .SelectMany(static x => x.GetTypes())
                .Where(type =>
                    !type.IsAbstract &&
                    !type.IsInterface &&
                    typeof(IAiPromptProvider).IsAssignableFrom(type) &&
                    type.GetCustomAttributes(typeof(AiPromptProviderAttribute), false).Length > 0)
                .ToArray();

            if (providerTypes.Length == 0)
            {
                throw new InvalidOperationException(
                    "No AI prompt providers were discovered in the specified assemblies.");
            }

            foreach (var providerType in providerTypes)
            {
                services.AddScoped(providerType);
            }

            services.AddScoped<IAiPromptProviderResolver>(sp =>
                new DefaultAiPromptProviderResolver(sp, assemblies));

            return services;
        }
    }
}