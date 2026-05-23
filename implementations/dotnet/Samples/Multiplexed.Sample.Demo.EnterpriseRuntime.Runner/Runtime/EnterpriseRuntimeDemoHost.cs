using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI;
using Multiplexed.AI.DI.Cleanup;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.DI.Persistence;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.Execution.Retention.Policies;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime.DI;
using Multiplexed.Realtime.DI;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Configuration;
using StackExchange.Redis;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime
{
    /// <summary>
    /// Builds a production-like service provider for the enterprise runtime demo.
    /// </summary>
    public sealed class EnterpriseRuntimeDemoHost : IAsyncDisposable
    {
        private readonly ServiceProvider _rootProvider;
        private readonly IServiceScope _scope;
        private readonly IReadOnlyCollection<IHostedService> _hostedServices;

        private EnterpriseRuntimeDemoHost(
            ServiceProvider rootProvider,
            IServiceScope scope,
            IReadOnlyCollection<IHostedService> hostedServices)
        {
            _rootProvider = rootProvider ?? throw new ArgumentNullException(
                nameof(rootProvider));

            _scope = scope ?? throw new ArgumentNullException(
                nameof(scope));

            _hostedServices = hostedServices ?? throw new ArgumentNullException(
                nameof(hostedServices));
        }

        /// <summary>
        /// Gets the scoped service provider used by the demo runner.
        /// </summary>
        public IServiceProvider ServiceProvider => _scope.ServiceProvider;

        /// <summary>
        /// Creates the enterprise runtime demo host.
        /// </summary>
        /// <param name="options">
        /// The runtime engine options.
        /// </param>
        /// <param name="settings">
        /// The enterprise runtime demo settings.
        /// </param>
        /// <param name="configureServices">
        /// The optional demo service configuration delegate.
        /// </param>
        /// <returns>
        /// The enterprise runtime demo host.
        /// </returns>
        public static EnterpriseRuntimeDemoHost Create(
            AiEngineOptions options,
            EnterpriseRuntimeDemoSettings settings,
            Action<IServiceCollection>? configureServices = null)
        {
            ArgumentNullException.ThrowIfNull(
                options);

            ArgumentNullException.ThrowIfNull(
                settings);

            var services = new ServiceCollection();

            RegisterServices(
                services,
                options,
                settings);

            configureServices?.Invoke(
                services);

            var rootProvider = services.BuildServiceProvider();
            var scope = rootProvider.CreateScope();

            var accessor = scope.ServiceProvider.GetRequiredService<IExecutionContextAccessor>();

            if (accessor is not DemoExecutionContextAccessor demoAccessor)
            {
                throw new InvalidOperationException(
                    $"Resolved execution context accessor is '{accessor.GetType().FullName}', expected '{typeof(DemoExecutionContextAccessor).FullName}'.");
            }

            demoAccessor.Set(
                CreateRuntimeContext());

            var hostedServices = rootProvider
                .GetServices<IHostedService>()
                .ToArray();

            foreach (var hostedService in hostedServices)
            {
                hostedService.StartAsync(
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }

            return new EnterpriseRuntimeDemoHost(
                rootProvider,
                scope,
                hostedServices);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            foreach (var hostedService in _hostedServices.Reverse())
            {
                await hostedService.StopAsync(
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (_rootProvider.GetService<IConnectionMultiplexer>() is IAsyncDisposable asyncMultiplexer)
            {
                await asyncMultiplexer.DisposeAsync()
                    .ConfigureAwait(false);
            }
            else if (_rootProvider.GetService<IConnectionMultiplexer>() is IDisposable multiplexer)
            {
                multiplexer.Dispose();
            }

            _scope.Dispose();

            if (_rootProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync()
                    .ConfigureAwait(false);

                return;
            }

            _rootProvider.Dispose();
        }

        /// <summary>
        /// Registers enterprise runtime demo services.
        /// </summary>
        /// <param name="services">
        /// The service collection.
        /// </param>
        /// <param name="options">
        /// The runtime engine options.
        /// </param>
        /// <param name="settings">
        /// The enterprise runtime demo settings.
        /// </param>
        private static void RegisterServices(
            IServiceCollection services,
            AiEngineOptions options,
            EnterpriseRuntimeDemoSettings settings)
        {
            ArgumentNullException.ThrowIfNull(
                services);

            ArgumentNullException.ThrowIfNull(
                options);

            ArgumentNullException.ThrowIfNull(
                settings);

            services.AddLogging();
            services.AddMemoryCache();

            services.AddSingleton(
                settings);

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(
                    settings.Redis.ConnectionString));

            EnsurePayloadStoreOptions(
                options,
                settings);

            services.AddMultiplexAI(
                options);

            services.AddAiPoliciesFromAssemblies(
                typeof(AiRuntimeAssemblyMarker).Assembly,
                typeof(CompactAiRetentionPolicy).Assembly);

            services.AddAiExecutionCleanup(cleanup =>
            {
                cleanup.AutoCleanupOnCompleted = options.Cleanup.AutoCleanupOnCompleted;
                cleanup.AutoCleanupOnFailed = options.Cleanup.AutoCleanupOnFailed;
                cleanup.SuppressSnapshotIfExist = options.Cleanup.SuppressSnapshotIfExist;
                cleanup.SuppressCleanupExceptions = options.Cleanup.SuppressCleanupExceptions;
            });

            services.AddAiExecutionReplay();

            var configuration = new ConfigurationManager();

            services.AddMultiplexedRbacRuntime(
                configuration,
                rbacOptions =>
                {
                    rbacOptions.MaxInFlightPerContextKey = 10;
                    rbacOptions.AllowClientMaxInFlightOverride = true;
                    rbacOptions.DemoMaxInFlightHeader = "X-Demo-Max-InFlight";
                    rbacOptions.InFlightCounterTtl = TimeSpan.FromSeconds(30);
                    rbacOptions.LogConcurrencyViolations = true;
                    rbacOptions.UseRedisLuaScriptShaCaching = true;
                    rbacOptions.AllowClientRotationOverlapOverride = true;
                    rbacOptions.RotationOverlapWindowHeader = "X-Demo-Rotation-Overlap-Ms";
                    rbacOptions.RotationOverlapWindow = TimeSpan.FromMilliseconds(10000);
                });

            services.AddMultiplexRealtime(
                    configureChannel: null,
                    typeof(AiRuntimeAssemblyMarker).Assembly)
                .AddSignalRRealtimeTransport(realtimeOptions =>
                {
                    realtimeOptions.CorsPolicy = "SignalRcors";
                    realtimeOptions.AllowedOrigins =
                    [
                        "http://localhost:3000"
                    ];
                });

            services.Replace(
                ServiceDescriptor.Singleton<IExecutionContextAccessor, DemoExecutionContextAccessor>());
        }

        /// <summary>
        /// Ensures payload store options are configured for the enterprise runtime demo.
        /// </summary>
        /// <param name="options">
        /// The runtime engine options.
        /// </param>
        /// <param name="settings">
        /// The enterprise runtime demo settings.
        /// </param>
        private static void EnsurePayloadStoreOptions(
            AiEngineOptions options,
            EnterpriseRuntimeDemoSettings settings)
        {
            ArgumentNullException.ThrowIfNull(
                options);

            ArgumentNullException.ThrowIfNull(
                settings);

            options.PayloadStore ??= new AiPayloadStoreOptions();

            options.PayloadStore.Enabled = true;

            if (string.IsNullOrWhiteSpace(options.PayloadStore.Provider) ||
                string.Equals(
                    options.PayloadStore.Provider,
                    "inmemory",
                    StringComparison.OrdinalIgnoreCase))
            {
                options.PayloadStore.Provider = "mongo-redis";
            }

            options.PayloadStore.RequireReplaySafePayloads = true;

            if (options.PayloadStore.MaxInlineSizeBytes <= 0)
            {
                options.PayloadStore.MaxInlineSizeBytes = 512;
            }

            options.PayloadStore.Mongo ??= new MongoAiPayloadStoreOptions();
            options.PayloadStore.Mongo.Enabled = true;
            options.PayloadStore.Mongo.ConnectionString ??= settings.Mongo.ConnectionString;
            options.PayloadStore.Mongo.DatabaseName ??= settings.Mongo.DatabaseName;
            options.PayloadStore.Mongo.CollectionName ??= $"payloads_demo_{Guid.NewGuid():N}";

            options.PayloadStore.RedisCache ??= new RedisAiPayloadCacheOptions();
            options.PayloadStore.RedisCache.Enabled = true;
            options.PayloadStore.RedisCache.KeyPrefix ??= $"demo:ai:payload:{Guid.NewGuid():N}";

            if (options.PayloadStore.RedisCache.ExpirationSeconds <= 0)
            {
                options.PayloadStore.RedisCache.ExpirationSeconds = 120;
            }

            if (options.PayloadStore.RedisCache.MaxCacheablePayloadBytes <= 0)
            {
                options.PayloadStore.RedisCache.MaxCacheablePayloadBytes = 1024 * 1024;
            }

            options.PayloadStore.StepIndexCache ??= new RedisAiStepPayloadIndexCacheOptions();
            options.PayloadStore.StepIndexCache.Enabled = true;
            options.PayloadStore.StepIndexCache.KeyPrefix ??= $"demo:ai:step-index:{Guid.NewGuid():N}";

            if (options.PayloadStore.StepIndexCache.ExpirationSeconds <= 0)
            {
                options.PayloadStore.StepIndexCache.ExpirationSeconds = 120;
            }

            options.PayloadStore.StepIndexCache.RefreshTtlOnRead = true;
        }

        /// <summary>
        /// Creates the demo execution context.
        /// </summary>
        /// <returns>
        /// The demo execution context.
        /// </returns>
        private static ExecutionContext CreateRuntimeContext()
        {
            return new ExecutionContext
            {
                ContextKey = string.Empty,
                Project = "Project",
                TenantId = "tenant-id-demo",
                TenantGroupId = "tenant-group-id-demo",
                CurrentNamespace = "Namespace",
                UserId = "demo-user",
                Namespaces = new List<NamespaceEntry>
                {
                    new()
                    {
                        Name = "Namespace",
                        Trns = new HashSet<string>
                        {
                            "trn:Project:demo:runtime:execution:read",
                            "trn:Project:demo:runtime:execution:run"
                        }
                    }
                },
                TtlSeconds = 300
            };
        }
    }
}