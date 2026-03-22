using System.Reflection;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MultiplexedRbac.Runtime.Realtime.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Dispatching;
using MultiplexedRbac.Runtime.Realtime.Events;
using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Transports;
using MultiplexedRbac.Runtime.Realtime.Transports.SignalR;
using MultiplexedRbac.Runtime.Realtime.Transports.Null;
using MultiplexedRbac.Runtime.Realtime.Context;
using MultiplexedRbac.Runtime.Realtime.Handlers;

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
        services.TryAddScoped<IRuntimeEventHandlerDispatcher, RuntimeEventHandlerDispatcher>();
        services.AddHostedService<RuntimeEventWorker>();

        services.TryAddSingleton<NullRealtimeTransport>();

        services.TryAddSingleton<IRealtimeTransportHost>(sp =>
            new RealtimeTransportHost<NullRealtimeTransport>(
                sp.GetRequiredService<NullRealtimeTransport>()));

        var resolvedAssemblies = assemblies is { Length: > 0 }
            ? assemblies
            : new[] { typeof(IRuntimeEvent).Assembly };

        RegisterRealtimeReducers(services, resolvedAssemblies);

        return services;
    }

    public static IServiceCollection AddSignalRRealtimeProvider(
            this IServiceCollection services,
            Action<SignalRRealtimeTransportOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SignalRRealtimeTransportOptions();
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

        services.TryAddSingleton<SignalRRealtimeTransport>();

        services.Replace(ServiceDescriptor.Singleton<IRealtimeTransportHost>(sp =>
            new RealtimeTransportHost<SignalRRealtimeTransport>(
                sp.GetRequiredService<SignalRRealtimeTransport>())));

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

            services.Replace(ServiceDescriptor.Singleton<IUserIdProvider, SignalRRealtimeTransportAdapter>());
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
            var serviceType = typeof(IRuntimeEventHandler<>).MakeGenericType(eventType);
            var implementationType = typeof(RealtimeDispatchHandler<>).MakeGenericType(eventType);

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