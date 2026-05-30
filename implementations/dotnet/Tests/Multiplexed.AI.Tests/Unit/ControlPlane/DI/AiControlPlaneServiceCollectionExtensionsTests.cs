using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Multiplexed.Abstractions.AI.ControlPlane.Observability;
using Multiplexed.Abstractions.AI.ControlPlane.Replay;
using Multiplexed.AI.Runtime.ControlPlane.DI;
using Multiplexed.AI.Runtime.ControlPlane.Observability;
using Multiplexed.AI.Runtime.Observability.Logging;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.DI
{
    public sealed class AiControlPlaneServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddAiControlPlane_Should_Register_Noop_Observer_By_Default()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddAiControlPlane();

            using var provider = services.BuildServiceProvider();

            var observer = provider.GetRequiredService<IAiControlPlaneObserver>();

            Assert.IsType<NoopAiControlPlaneObserver>(observer);
        }

        [Fact]
        public void AddAiControlPlaneLogging_Should_Replace_Noop_Observer_With_Logged_Observer()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services
                .AddAiControlPlane()
                .AddAiControlPlaneLogging();

            using var provider = services.BuildServiceProvider();

            var observer = provider.GetRequiredService<IAiControlPlaneObserver>();
            var logger = provider.GetRequiredService<IAiControlPlaneLogger>();

            Assert.IsType<LoggedAiControlPlaneObserver>(observer);
            Assert.IsType<AiControlPlaneLogger>(logger);
        }

        [Fact]
        public void AddAiControlPlane_Should_Register_Replay_ControlPlane()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddAiControlPlane();

            var descriptor = services.SingleOrDefault(service =>
                service.ServiceType == typeof(IAiReplayControlPlane));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void AddAiControlPlaneLogging_Should_Not_Register_Duplicate_Observers()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services
                .AddAiControlPlane()
                .AddAiControlPlaneLogging();

            var observerDescriptors = services
                .Where(service => service.ServiceType == typeof(IAiControlPlaneObserver))
                .ToArray();

            Assert.Single(observerDescriptors);
            Assert.Equal(
                typeof(LoggedAiControlPlaneObserver),
                observerDescriptors[0].ImplementationType);
        }
    }
}