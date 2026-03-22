using Microsoft.Extensions.DependencyInjection;
using Multiplexed.AI.Abstractions;
using Multiplexed.Abstractions.AI;
using Multiplexed.AI.Providers;

namespace Multiplexed.AI.DI
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMultiplexAI(this IServiceCollection services)
        {
            services.AddSingleton<IAIProvider, FakeAIProvider>();
            services.AddSingleton<IAIService, AIService>();

            return services;
        }
    }
}