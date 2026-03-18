using System.Reflection;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MultiplexedRbac.Runtime.Realtime;
using MultiplexedRbac.Runtime.Realtime.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Dispatching;
using MultiplexedRbac.Runtime.Realtime.Events;
using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Providers;
using MultiplexedRbac.Runtime.Realtime.Providers.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Providers.SignalR;
using MultiplexedRbac.Runtime.Realtime.Reducers;

namespace MultiplexedRbac.Runtime.Realtime.DI;

/// <summary>
/// Registers the core realtime runtime infrastructure with the default
/// null provider.
///
/// This includes:
/// - runtime event dispatcher
/// - null realtime provider
/// - generic provider host
/// - automatic reducer registration for realtime events
/// Launch
/// builder.Services
///     .AddMultiplexRealtime()
///     .AddSignalRRealtimeProvider();
///     //.AddWebSocketRealtimeProvider();
/// var app = builder.Build();
/// app.MapControllers();
/// // app.UseWebSockets(); // in case of WebSockets
/// app.MapMultiplexRealtime("/runtime/live");
/// app.Run();
/// </summary>
public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddMultiplexRealtime(
        this IServiceCollection services,
        Action<RuntimeEventChannelOptions>? configureChannel = null,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var channelOptions = new RuntimeEventChannelOptions();
        configureChannel?.Invoke(channelOptions);

        var channel = Channel.CreateBounded<IRuntimeEvent>(
            new BoundedChannelOptions(channelOptions.Capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = channelOptions.SingleReader,
                SingleWriter = channelOptions.SingleWriter,
                AllowSynchronousContinuations = channelOptions.AllowSynchronousContinuations
            });

        services.TryAddSingleton(channel);

        services.TryAddSingleton<IRealtimeEventContext, RealtimeEventContext>();
        services.TryAddSingleton<IRuntimeEventDispatcher, RuntimeEventDispatcher>();
        services.TryAddScoped<IRuntimeEventReducerDispatcher, RuntimeEventReducerDispatcher>();
        services.AddHostedService<RuntimeEventWorker>();

        services.TryAddSingleton<NullRealtimeProvider>();

        services.TryAddSingleton<IRealtimeProviderHost>(sp =>
            new RealtimeProviderHost<NullRealtimeProvider>(
                sp.GetRequiredService<NullRealtimeProvider>()));

        var resolvedAssemblies = assemblies is { Length: > 0 }
            ? assemblies
            : new[] { typeof(IRuntimeEvent).Assembly };

        RegisterRealtimeReducers(services, resolvedAssemblies);

        return services;
    }

    public static IServiceCollection AddSignalRRealtimeProvider(
            this IServiceCollection services,
            Action<SignalRRealtimeProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SignalRRealtimeProviderOptions();
        configure(options);

        if (options.AllowedOrigins is null || options.AllowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "SignalRRealtimeProviderOptions.AllowedOrigins must contain at least one origin.");
        }

        services.AddCors(cors =>
        {
            cors.AddPolicy(options.CorsPolicy, policy =>
            {
                policy
                    .WithOrigins(options.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddSignalR();

        services.TryAddSingleton<SignalRRealtimeProvider>();

        services.Replace(ServiceDescriptor.Singleton<IRealtimeProviderHost>(sp =>
            new RealtimeProviderHost<SignalRRealtimeProvider>(
                sp.GetRequiredService<SignalRRealtimeProvider>())));

        /*
         * Optional user identifier resolution strategy.
         *
         * If configured, the runtime registers:
         * - the transport-agnostic resolver
         * - the SignalR IUserIdProvider adapter
         *
         * This enables Clients.User(userId) routing.
         */
        if (options.UserIdentifierResolverType is not null)
        {
            services.Replace(ServiceDescriptor.Singleton(
                typeof(IRealtimeUserIdentifierResolver),
                options.UserIdentifierResolverType));

            services.Replace(ServiceDescriptor.Singleton<IUserIdProvider, SignalRUserIdProviderAdapter>());
        }

        return services;
    }

    private static void RegisterRealtimeReducers(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies)
    {
        var eventTypes = assemblies
            .SelectMany(SafeGetTypes)
            .Where(IsRealtimeDispatchableEvent)
            .ToArray();

        foreach (var eventType in eventTypes)
        {
            var serviceType = typeof(IRuntimeEventReducer<>).MakeGenericType(eventType);
            var implementationType = typeof(RealtimeDispatchReducer<>).MakeGenericType(eventType);

            services.TryAddScoped(serviceType, implementationType);
        }
    }

    private static bool IsRealtimeDispatchableEvent(Type type)
    {
        return type is { IsClass: true, IsAbstract: false }
            && typeof(IRuntimeEvent).IsAssignableFrom(type)
            && type.GetCustomAttributes(typeof(RealtimeEventAttribute), inherit: false).Length > 0;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}