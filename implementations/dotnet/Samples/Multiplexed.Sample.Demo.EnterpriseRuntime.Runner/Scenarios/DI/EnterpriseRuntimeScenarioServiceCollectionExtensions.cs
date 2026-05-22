using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.DI
{
    /// <summary>
    /// Provides enterprise runtime scenario registration extensions.
    /// </summary>
    public static class EnterpriseRuntimeScenarioServiceCollectionExtensions
    {
        /// <summary>
        /// Registers enterprise runtime scenarios from assemblies.
        /// </summary>
        /// <param name="services">
        /// The service collection.
        /// </param>
        /// <param name="assemblies">
        /// The scenario assemblies.
        /// </param>
        /// <returns>
        /// The service collection.
        /// </returns>
        public static IServiceCollection AddEnterpriseRuntimeScenariosFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(
                services);

            ArgumentNullException.ThrowIfNull(
                assemblies);

            var scenarioTypes = assemblies
                .Distinct()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    type is
                    {
                        IsAbstract: false,
                        IsInterface: false
                    } &&
                    typeof(IEnterpriseRuntimeScenario).IsAssignableFrom(type));

            foreach (var scenarioType in scenarioTypes)
            {
                services.AddSingleton(
                    typeof(IEnterpriseRuntimeScenario),
                    scenarioType);
            }

            return services;
        }
    }
}