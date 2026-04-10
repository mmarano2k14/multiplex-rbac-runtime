using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Prompt;

namespace Multiplexed.AI.Runtime.AI.Providers.Llm
{
    /// <summary>
    /// Default implementation of <see cref="IAiPromptProviderResolver"/>.
    ///
    /// PURPOSE:
    /// - Resolves AI prompt providers from the current DI scope
    /// - Builds provider key to concrete type mapping from scanned assemblies
    /// - Mirrors the same architectural approach as the assembly-based AI step registry
    ///
    /// IMPORTANT:
    /// - Provider identity comes only from <see cref="AiPromptProviderAttribute"/>
    /// - Provider instances are resolved by concrete type from the current scope
    /// </summary>
    public sealed class DefaultAiPromptProviderResolver : IAiPromptProviderResolver
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IReadOnlyDictionary<string, Type> _providerTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiPromptProviderResolver"/> class.
        /// </summary>
        /// <param name="serviceProvider">
        /// The current scoped service provider.
        /// </param>
        /// <param name="assemblies">
        /// The assemblies to scan for AI prompt providers.
        /// </param>
        public DefaultAiPromptProviderResolver(
            IServiceProvider serviceProvider,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(assemblies);

            _serviceProvider = serviceProvider;

            var discoveredProviders = assemblies
                .Distinct()
                .SelectMany(static x => x.GetTypes())
                .Where(type =>
                    !type.IsAbstract &&
                    !type.IsInterface &&
                    typeof(IAiPromptProvider).IsAssignableFrom(type))
                .Select(type => new
                {
                    Type = type,
                    Attribute = type.GetCustomAttribute<AiPromptProviderAttribute>(inherit: false)
                })
                .Where(x => x.Attribute is not null)
                .ToArray();

            if (discoveredProviders.Length == 0)
            {
                throw new InvalidOperationException(
                    "No AI prompt providers were discovered.");
            }

            var duplicate = discoveredProviders
                .GroupBy(x => x.Attribute!.ProviderKey, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(static g => g.Count() > 1);

            if (duplicate is not null)
            {
                throw new InvalidOperationException(
                    $"Multiple AI prompt providers were discovered with the same provider key '{duplicate.Key}'.");
            }

            _providerTypes = discoveredProviders.ToDictionary(
                x => x.Attribute!.ProviderKey,
                x => x.Type,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public IAiPromptProvider Resolve(string providerKey)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                throw new ArgumentException("Provider key is required.", nameof(providerKey));
            }

            if (!_providerTypes.TryGetValue(providerKey, out var providerType))
            {
                throw new KeyNotFoundException(
                    $"No AI prompt provider is registered for provider key '{providerKey}'.");
            }

            var provider = _serviceProvider.GetRequiredService(providerType) as IAiPromptProvider;
            if (provider is null)
            {
                throw new InvalidOperationException(
                    $"AI prompt provider type '{providerType.FullName}' is not registered correctly in the service container.");
            }

            return provider;
        }
    }
}