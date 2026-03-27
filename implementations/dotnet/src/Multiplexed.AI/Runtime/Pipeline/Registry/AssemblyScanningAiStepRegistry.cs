using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline.Registry
{
    /// <summary>
    /// Resolves AI steps by scanning configured assemblies for types decorated with <see cref="AiStepAttribute"/>.
    /// </summary>
    public sealed class AssemblyScanningAiStepRegistry : IAiStepRegistry
    {
        private readonly IServiceProvider _services;
        private readonly IReadOnlyDictionary<string, Type> _stepTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyScanningAiStepRegistry"/> class.
        /// </summary>
        /// <param name="services">The runtime service provider used to create step instances.</param>
        /// <param name="assemblies">The assemblies to scan for AI steps.</param>
        public AssemblyScanningAiStepRegistry(
            IServiceProvider services,
            IEnumerable<Assembly> assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assemblies);

            _services = services;
            _stepTypes = BuildStepMap(assemblies);
        }

        /// <summary>
        /// Resolves the runtime step instance for the specified declarative step key.
        /// </summary>
        /// <param name="stepKey">The unique declarative step key.</param>
        /// <returns>The matching runtime step instance.</returns>
        public IAiStep Resolve(string stepKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);

            if (!_stepTypes.TryGetValue(stepKey, out var stepType))
            {
                throw new InvalidOperationException(
                    $"Step '{stepKey}' is not registered.");
            }

            return (IAiStep)_services.GetRequiredService(stepType);
        }

        private static IReadOnlyDictionary<string, Type> BuildStepMap(IEnumerable<Assembly> assemblies)
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in assemblies.Distinct())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;

                    if (!typeof(IAiStep).IsAssignableFrom(type))
                        continue;

                    var attribute = type.GetCustomAttribute<AiStepAttribute>();
                    if (attribute is null)
                        continue;

                    if (map.ContainsKey(attribute.StepKey))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate AI step key '{attribute.StepKey}' detected for type '{type.FullName}'.");
                    }

                    map[attribute.StepKey] = type;
                }
            }

            return map;
        }
    }
}