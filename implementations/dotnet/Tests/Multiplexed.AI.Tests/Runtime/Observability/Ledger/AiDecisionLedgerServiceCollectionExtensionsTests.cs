using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.AI.Observability.Ledger;
using Multiplexed.AI.Runtime.Observability.Ledger.DI;
using Multiplexed.AI.Runtime.Observability.Ledger.Mongo;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Observability.Ledger
{
    /// <summary>
    /// Tests dependency injection registration for the AI decision ledger.
    /// </summary>
    public sealed class AiDecisionLedgerServiceCollectionExtensionsTests
    {
        /// <summary>
        /// Verifies that the default registration resolves a no-operation ledger and default recorder.
        /// </summary>
        [Fact]
        public void AddAiDecisionLedger_ShouldRegisterDefaultServices()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddAiDecisionLedger();

            using var provider = services.BuildServiceProvider();

            var ledger = provider.GetRequiredService<IAiDecisionLedger>();
            var recorder = provider.GetRequiredService<IAiDecisionLedgerRecorder>();

            ledger.Should().BeOfType<NoOpAiDecisionLedger>();
            recorder.Should().BeOfType<DefaultAiDecisionLedgerRecorder>();
        }

        /// <summary>
        /// Verifies that the in-memory registration resolves an in-memory ledger.
        /// </summary>
        [Fact]
        public void AddInMemoryAiDecisionLedger_ShouldRegisterInMemoryLedger()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddInMemoryAiDecisionLedger();

            using var provider = services.BuildServiceProvider();

            var ledger = provider.GetRequiredService<IAiDecisionLedger>();
            var recorder = provider.GetRequiredService<IAiDecisionLedgerRecorder>();

            ledger.Should().BeOfType<InMemoryAiDecisionLedger>();
            recorder.Should().BeOfType<DefaultAiDecisionLedgerRecorder>();
        }

        /// <summary>
        /// Verifies that disabled registration resolves a no-operation recorder.
        /// </summary>
        [Fact]
        public void AddDisabledAiDecisionLedger_ShouldRegisterNoOpRecorder()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddDisabledAiDecisionLedger();

            using var provider = services.BuildServiceProvider();

            var ledger = provider.GetRequiredService<IAiDecisionLedger>();
            var recorder = provider.GetRequiredService<IAiDecisionLedgerRecorder>();

            ledger.Should().BeOfType<NoOpAiDecisionLedger>();
            recorder.Should().BeOfType<NoOpAiDecisionLedgerRecorder>();
        }

        /// <summary>
        /// Verifies that Mongo storage mode registers the MongoDB-backed decision ledger.
        /// </summary>
        [Fact]
        public void AddAiDecisionLedger_WithMongoStorageMode_ShouldRegisterMongoLedger()
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddAiDecisionLedger(options =>
            {
                options.WriteMode = AiDecisionLedgerWriteMode.BestEffort;
                options.StorageMode = AiDecisionLedgerStorageMode.Mongo;
            });

            var descriptor = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IAiDecisionLedger));

            descriptor.Should().NotBeNull();
            descriptor!.ImplementationType.Should().Be(typeof(MongoAiDecisionLedger));
        }
    }
}