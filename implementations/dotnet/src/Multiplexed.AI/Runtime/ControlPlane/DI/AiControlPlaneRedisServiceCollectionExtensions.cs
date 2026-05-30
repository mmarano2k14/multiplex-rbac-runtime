using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.AI.Redis.ControlPlane.SharedController;
using Multiplexed.AI.Runtime.ControlPlane.SharedController;
using StackExchange.Redis;

namespace Multiplexed.AI.Runtime.ControlPlane.DI
{
    /// <summary>
    /// Provides Redis-backed dependency injection registration for AI runtime control-plane services.
    /// </summary>
    public static class AiControlPlaneRedisServiceCollectionExtensions
    {
        /// <summary>
        /// Replaces the default in-memory shared run store with the Redis-backed shared run store.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional Redis shared run store options configuration.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <remarks>
        /// This method does not register the Redis connection itself.
        /// The application must already register <see cref="IConnectionMultiplexer"/>.
        ///
        /// Expected usage:
        ///
        /// services.AddSingleton&lt;IConnectionMultiplexer&gt;(
        ///     _ =&gt; ConnectionMultiplexer.Connect("localhost:6379"));
        ///
        /// services.AddAiControlPlane();
        /// services.AddRedisAiSharedRunStore();
        ///
        /// The default <see cref="IAiSharedRunStore"/> registered by AddAiControlPlane
        /// is replaced by <see cref="RedisAiSharedRunStore"/>.
        /// </remarks>
        public static IServiceCollection AddRedisAiSharedRunStore(
            this IServiceCollection services,
            Action<RedisAiSharedRunStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (configure is null)
            {
                services.AddOptions<RedisAiSharedRunStoreOptions>();
            }
            else
            {
                services.Configure(configure);
            }

            services.RemoveAll<IAiSharedRunStore>();
            services.TryAddSingleton<IAiSharedRunStore, RedisAiSharedRunStore>();

            return services;
        }

        /// <summary>
        /// Registers a Redis connection multiplexer and replaces the default in-memory
        /// shared run store with the Redis-backed shared run store.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">The Redis connection string.</param>
        /// <param name="configure">Optional Redis shared run store options configuration.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <remarks>
        /// This overload is convenient for demos, tests, and simple host setups.
        /// Larger applications may prefer to register <see cref="IConnectionMultiplexer"/>
        /// themselves and call <see cref="AddRedisAiSharedRunStore(IServiceCollection, Action{RedisAiSharedRunStoreOptions}?)"/>.
        /// </remarks>
        public static IServiceCollection AddRedisAiSharedRunStore(
            this IServiceCollection services,
            string connectionString,
            Action<RedisAiSharedRunStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "Redis connection string cannot be null or empty.",
                    nameof(connectionString));
            }

            services.TryAddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(connectionString));

            return services.AddRedisAiSharedRunStore(configure);
        }
    }
}