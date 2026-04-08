using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Persistence.Mongo;
using Multiplexed.AI.Runtime.Execution.Persistence.Mongo;
using System;

namespace Multiplexed.AI.Runtime.DependencyInjection
{
    /// <summary>
    /// Registers AI execution snapshot persistence based on the configured engine options.
    ///
    /// DESIGN:
    /// - Snapshot persistence remains optional
    /// - Provider selection is driven by <see cref="AiEngineOptions"/>
    /// - The execution engine stays decoupled from provider-specific wiring
    ///
    /// CURRENT PROVIDERS:
    /// - MongoDB
    ///
    /// FUTURE EXTENSIBILITY:
    /// Additional providers can be added here later without changing engine code.
    /// </summary>
    public static class AiExecutionSnapshotServiceCollectionExtensions
    {
        /// <summary>
        /// Registers execution snapshot persistence services for the configured provider.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The engine options.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddAiExecutionSnapshots(
            this IServiceCollection services,
            AiEngineOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            if (!options.Snapshots.Enabled)
            {
                return services;
            }

            if (options.Snapshots.Mongo.Enabled)
            {
                services.AddMongoAiExecutionSnapshots<ExecutionContextSnapshot>(mongo =>
                {
                    mongo.ConnectionString = options.Snapshots.Mongo.ConnectionString;
                    mongo.DatabaseName = options.Snapshots.Mongo.DatabaseName;
                    mongo.CollectionName = options.Snapshots.Mongo.CollectionName;
                });

                return services;
            }

            throw new InvalidOperationException(
                "Execution snapshots are enabled, but no supported snapshot provider is configured.");
        }
    }
}