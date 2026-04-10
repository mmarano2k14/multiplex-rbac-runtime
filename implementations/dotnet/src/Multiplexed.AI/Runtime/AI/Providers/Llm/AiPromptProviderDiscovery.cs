using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Multiplexed.Abstractions.AI.Prompt;

namespace Multiplexed.AI.Runtime.AI.Providers.Llm
{
    /// <summary>
    /// Discovers AI prompt providers from assemblies using <see cref="AiPromptProviderAttribute"/>.
    ///
    /// PURPOSE:
    /// - Scans assemblies for concrete provider implementations
    /// - Extracts provider metadata from attributes
    /// - Produces a normalized descriptor collection for DI registration and resolution
    /// </summary>
    public static class AiPromptProviderDiscovery
    {
        /// <summary>
        /// Discovers all valid AI prompt providers from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan.
        /// </param>
        /// <returns>
        /// A read-only collection of discovered provider descriptors.
        /// </returns>
        public static IReadOnlyCollection<AiPromptProviderDescriptor> Discover(params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            var descriptors = new List<AiPromptProviderDescriptor>();

            foreach (var assembly in assemblies.Where(static a => a is not null))
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    if (!typeof(IAiPromptProvider).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    var attribute = type.GetCustomAttribute<AiPromptProviderAttribute>();
                    if (attribute is null)
                    {
                        continue;
                    }

                    descriptors.Add(new AiPromptProviderDescriptor(
                        attribute.ProviderKey,
                        type));
                }
            }

            return descriptors;
        }
    }
}