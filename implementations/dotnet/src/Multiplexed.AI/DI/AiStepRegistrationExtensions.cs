using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Pipeline.Registry;

namespace Multiplexed.AI.DI
{
    /// <summary>
    /// Registers AI steps discovered through assembly scanning.
    /// </summary>
    public static class AiStepRegistrationExtensions
    {
        /// <summary>
        /// Scans the supplied assemblies for AI steps decorated with <see cref="AiStepAttribute"/>
        /// and registers both the step implementations and the step registry.
        /// </summary>
        /// <param name="services">Target service collection.</param>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The same service collection instance.</returns>
        public static IServiceCollection AddAiStepsFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assemblies);

            var stepTypes = assemblies
                .Distinct()
                .SelectMany(x => x.GetTypes())
                .Where(type =>
                    !type.IsAbstract &&
                    !type.IsInterface &&
                    typeof(IAiStep).IsAssignableFrom(type) &&
                    type.GetCustomAttributes(typeof(AiStepAttribute), false).Length > 0)
                .ToArray();

            foreach (var stepType in stepTypes)
            {
                services.AddScoped(stepType);
            }

            services.AddScoped<IAiStepRegistry>(sp =>
                new AssemblyScanningAiStepRegistry(sp, assemblies));

            return services;
        }
    }
}