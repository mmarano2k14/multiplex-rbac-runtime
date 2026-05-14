using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Tests.Fixtures;
using Multiplexed.AI.Tests.Integration.Fixtures;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.AI.Tests.Runtime.Execution.Instance;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.MultiInstance
{
    /// <summary>
    /// Integration tests for multiple AI runtime instances participating in distributed DAG execution.
    /// </summary>
    [Collection("redis")]
    public sealed class MultiInstanceDagExecutionIntegrationTests
    {
        /// <summary>
        /// Verifies that multiple integration runtime hosts can be created with isolated service providers,
        /// different runtime instance identities, and resolvable DAG execution engines.
        /// </summary>
        [Fact]
        public async Task MultiInstanceIntegrationHosts_Should_Resolve_Engines_With_Different_Runtime_Instance_Identities()
        {
            await using var hostA = await CreateIntegrationHostAsync("runtime-instance-a");
            await using var hostB = await CreateIntegrationHostAsync("runtime-instance-b");
            await using var hostC = await CreateIntegrationHostAsync("runtime-instance-c");

            Assert.Equal("runtime-instance-a", hostA.RuntimeInstanceIdentity.RuntimeInstanceId);
            Assert.Equal("runtime-instance-b", hostB.RuntimeInstanceIdentity.RuntimeInstanceId);
            Assert.Equal("runtime-instance-c", hostC.RuntimeInstanceIdentity.RuntimeInstanceId);

            Assert.NotSame(hostA.ServiceProvider, hostB.ServiceProvider);
            Assert.NotSame(hostA.ServiceProvider, hostC.ServiceProvider);
            Assert.NotSame(hostB.ServiceProvider, hostC.ServiceProvider);

            Assert.NotNull(hostA.Engine);
            Assert.NotNull(hostB.Engine);
            Assert.NotNull(hostC.Engine);

            Assert.NotSame(hostA.Engine, hostB.Engine);
            Assert.NotSame(hostA.Engine, hostC.Engine);
            Assert.NotSame(hostB.Engine, hostC.Engine);
        }

        /// <summary>
        /// Verifies that the runtime instance identity is stable within a single integration host lifetime.
        /// </summary>
        [Fact]
        public async Task MultiInstanceIntegrationHost_Should_Keep_Same_Runtime_Instance_Identity_For_Host_Lifetime()
        {
            await using var host = await CreateIntegrationHostAsync("runtime-instance-a");

            var first = host.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();
            var second = host.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();

            Assert.Same(first, second);
            Assert.Equal("runtime-instance-a", first.RuntimeInstanceId);
            Assert.Equal(first.RuntimeInstanceId, second.RuntimeInstanceId);
        }

        /// <summary>
        /// Creates one fully wired integration runtime host with a deterministic runtime instance identity.
        /// </summary>
        /// <param name="runtimeInstanceId">The deterministic runtime instance identifier.</param>
        /// <returns>The created multi-instance integration host.</returns>
        private static async Task<MultiInstanceIntegrationHost> CreateIntegrationHostAsync(
            string runtimeInstanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);

            var host = await AiDagExecutionEngineFixture.CreateAsync(
                CreateOptions(),
                configureServices: services =>
                {
                    services.RemoveAll<IAiRuntimeInstanceIdentity>();
                    services.AddSingleton<IAiRuntimeInstanceIdentity>(
                        new TestAiRuntimeInstanceIdentity(runtimeInstanceId));
                });

            return new MultiInstanceIntegrationHost(
                runtimeInstanceId,
                host);
        }

        /// <summary>
        /// Creates AI engine options for multi-instance DAG execution integration tests.
        /// </summary>
        /// <returns>The configured AI engine options.</returns>
        private static AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                DefaultPipelineDefinitionSource = "InMemory"
            };
        }

        /// <summary>
        /// Represents one fully wired integration runtime host participating in multi-instance tests.
        /// </summary>
        private sealed class MultiInstanceIntegrationHost : IAsyncDisposable, IDisposable
        {
            private readonly AiDagExecutionEngineTestHost _innerHost;

            /// <summary>
            /// Initializes a new instance of the <see cref="MultiInstanceIntegrationHost"/> class.
            /// </summary>
            /// <param name="runtimeInstanceId">The expected runtime instance identifier.</param>
            /// <param name="innerHost">The underlying DAG execution engine test host.</param>
            public MultiInstanceIntegrationHost(
                string runtimeInstanceId,
                AiDagExecutionEngineTestHost innerHost)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);
                ArgumentNullException.ThrowIfNull(innerHost);

                RuntimeInstanceId = runtimeInstanceId;
                _innerHost = innerHost;

                RuntimeInstanceIdentity =
                    _innerHost.ServiceProvider.GetRequiredService<IAiRuntimeInstanceIdentity>();

                Engine = _innerHost.Engine;
            }

            /// <summary>
            /// Gets the expected runtime instance identifier.
            /// </summary>
            public string RuntimeInstanceId { get; }

            /// <summary>
            /// Gets the scoped service provider for this runtime host.
            /// </summary>
            public IServiceProvider ServiceProvider => _innerHost.ServiceProvider;

            /// <summary>
            /// Gets the resolved runtime instance identity.
            /// </summary>
            public IAiRuntimeInstanceIdentity RuntimeInstanceIdentity { get; }

            /// <summary>
            /// Gets the DAG execution engine.
            /// </summary>
            public AiDagExecutionEngine Engine { get; }

            /// <summary>
            /// Disposes the underlying runtime host.
            /// </summary>
            public void Dispose()
            {
                _innerHost.Dispose();
            }

            /// <summary>
            /// Asynchronously disposes the underlying runtime host.
            /// </summary>
            public async ValueTask DisposeAsync()
            {
                await _innerHost.DisposeAsync();
            }
        }
    }
}