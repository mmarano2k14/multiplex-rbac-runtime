using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI;
using Multiplexed.AI.DI.AI;
using Multiplexed.AI.DI.Engine;
using Multiplexed.AI.DI.Persistence;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Runtime.AI.Providers.Llm.OpenAI.DI;
using Multiplexed.AI.Runtime.DependencyInjection;
using Multiplexed.AI.Runtime.Execution.Engine;
using Multiplexed.AI.Runtime.Pipeline.Steps.Prompt;
using Multiplexed.AI.Tests.Fakes;
using Multiplexed.Rbac.Core.ExecutionContext;
using Multiplexed.Rbac.Core.Runtime.DI;
using Multiplexed.Rbac.Core.Runtime.Messaging.NServiceBus.DI;
using Multiplexed.Realtime.DI;
using StackExchange.Redis;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures
{
    /// <summary>
    /// Creates fully wired DAG execution engines using the production dependency injection graph.
    /// </summary>
    public static class AiDagExecutionEngineFixture
    {
        /// <summary>
        /// Creates a fully wired DAG execution engine integration test host.
        /// </summary>
        public static async Task<AiDagExecutionEngineTestHost> CreateAsync(
            AiEngineOptions options,
            string? mongoConnectionString = null,
            string? mongoDatabaseName = null,
            string? redisConnectionString = null)
        {
            ArgumentNullException.ThrowIfNull(options);

            var services = new ServiceCollection();

            RegisterProductionServices(
                services,
                options,
                mongoConnectionString,
                mongoDatabaseName,
                redisConnectionString);

            var rootProvider = services.BuildServiceProvider();
            var scope = rootProvider.CreateScope();
            var serviceProvider = scope.ServiceProvider;

            var accessor = serviceProvider.GetRequiredService<IExecutionContextAccessor>();

            if (accessor is not FakeInMemoryContextAccessor fakeAccessor)
            {
                throw new InvalidOperationException(
                    $"Resolved IExecutionContextAccessor is '{accessor.GetType().FullName}', expected '{typeof(FakeInMemoryContextAccessor).FullName}'.");
            }

            fakeAccessor.Set(CreateRuntimeContext());

            if (options.Snapshots.Enabled && options.Snapshots.Mongo.Enabled)
            {
                await serviceProvider.EnsureMongoAiExecutionSnapshotIndexesAsync<ExecutionContextSnapshot>(
                    options.Snapshots.Mongo);
            }

            var engine = serviceProvider.GetRequiredService<AiDagExecutionEngine>();

            return new AiDagExecutionEngineTestHost(
                rootProvider,
                scope,
                engine);
        }

        public static string DumpState(AiExecutionRecord? record, AiExecutionState? state)
        {
            if (record is null && state is null)
            {
                return "Record=null, State=null";
            }

            var lines = new List<string>
            {
                $"RecordStatus={record?.Status}",
                $"CurrentStep={record?.CurrentStep}",
                $"ExecutionStepKey={record?.ExecutionStepKey}",
                $"CompletedSteps=[{string.Join(", ", record?.CompletedSteps ?? new List<string>())}]"
            };

            if (state?.Steps is not null)
            {
                foreach (var step in state.Steps.Values.OrderBy(x => x.StepName, StringComparer.Ordinal))
                {
                    lines.Add(
                        $"Step={step.StepName}, " +
                        $"Status={step.Status}, " +
                        $"RetryCount={step.RetryCount}, " +
                        $"MaxRetries={step.MaxRetries}, " +
                        $"NextRetryAtUtc={step.NextRetryAtUtc}, " +
                        $"ClaimedBy={step.ClaimedBy}, " +
                        $"ClaimToken={step.ClaimToken}, " +
                        $"DependsOn=[{string.Join(", ", step.DependsOn ?? new List<string>())}]");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Creates a runtime RBAC context suitable for integration tests.
        /// </summary>
        private static ExecutionContext CreateRuntimeContext()
        {
            return new ExecutionContext
            {
                ContextKey = string.Empty,
                Project = "Project",
                TenantId = "tenant-id-xxxx",
                TenantGroupId = "tenant-group-id-xxx",
                CurrentNamespace = "Namespace",
                UserId = "userId",
                Namespaces = new List<NamespaceEntry>
                {
                    new NamespaceEntry
                    {
                        Name = "Namespace",
                        Trns = new HashSet<string>
                        {
                            "trn:Project:crm:billing:invoice:read",
                            "trn:Project:crm:billing:invoice:refund"
                        }
                    }
                },
                TtlSeconds = 300
            };
        }

        /// <summary>
        /// Registers the production runtime services required by the DAG execution engine.
        /// </summary>
        private static void RegisterProductionServices(
            IServiceCollection services,
            AiEngineOptions options,
            string? mongoConnectionString = null,
            string? mongoDatabaseName = null,
            string? redisConnectionString = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            services.AddLogging();
            services.AddMemoryCache();

            var resolvedRedisConnectionString =
                redisConnectionString ??
                "localhost:6379";

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(resolvedRedisConnectionString));

            services.AddMultiplexRealtime()
                .AddSignalRRealtimeTransport(realtimeOptions =>
                {
                    realtimeOptions.CorsPolicy = "SignalRcors";
                    realtimeOptions.AllowedOrigins =
                    [
                        "http://localhost:3000"
                    ];
                });

            // Full AI runtime registrations using strongly-typed options.
            services.AddMultiplexAI(options);

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
                    })
                .AddMultiplexedRbacHttp()
                .AddMultiplexedRbacNServiceBus()
                .AddAiPromptRuntime(typeof(AiRuntimeAssemblyMarker).Assembly)
                .AddOpenAiPromptProvider(options =>
                 {
                     options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                         ?? throw new InvalidOperationException("OPENAI_API_KEY is required.");
                 });

            // Use the same fake accessor strategy already used by other engine tests.
            services.Replace(ServiceDescriptor.Singleton<IExecutionContextAccessor, FakeInMemoryContextAccessor>());

            if (options.Snapshots.Enabled && options.Snapshots.Mongo.Enabled)
            {
                var connectionString =
                    mongoConnectionString ??
                    options.Snapshots.Mongo.ConnectionString ??
                    throw new InvalidOperationException(
                        "Mongo snapshot persistence is enabled but no connection string was provided.");

                var databaseName =
                    mongoDatabaseName ??
                    options.Snapshots.Mongo.DatabaseName ??
                    throw new InvalidOperationException(
                        "Mongo snapshot persistence is enabled but no database name was provided.");

                options.Snapshots.Mongo.ConnectionString = connectionString;
                options.Snapshots.Mongo.DatabaseName = databaseName;

                services.AddAiExecutionSnapshots(options);
                services.AddAiExecutionReplay();
                services.AddAiStepsFromAssemblies(typeof(AiRuntimeAssemblyMarker).Assembly);
                
 
            }
        }
    }

    /// <summary>
    /// Represents a fully built DAG execution engine integration test host.
    /// </summary>
    public sealed class AiDagExecutionEngineTestHost : IAsyncDisposable, IDisposable
    {
        public AiDagExecutionEngineTestHost(
            ServiceProvider rootProvider,
            IServiceScope scope,
            AiDagExecutionEngine engine)
        {
            RootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public ServiceProvider RootProvider { get; }

        public IServiceScope Scope { get; }

        public IServiceProvider ServiceProvider => Scope.ServiceProvider;

        public AiDagExecutionEngine Engine { get; }

        public void Dispose()
        {
            if (RootProvider.GetService<IConnectionMultiplexer>() is IDisposable multiplexer)
            {
                multiplexer.Dispose();
            }

            Scope.Dispose();
            RootProvider.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (RootProvider.GetService<IConnectionMultiplexer>() is IAsyncDisposable asyncMultiplexer)
            {
                await asyncMultiplexer.DisposeAsync();
            }
            else if (RootProvider.GetService<IConnectionMultiplexer>() is IDisposable multiplexer)
            {
                multiplexer.Dispose();
            }

            Scope.Dispose();

            if (RootProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
                return;
            }

            RootProvider.Dispose();
        }
    }
}