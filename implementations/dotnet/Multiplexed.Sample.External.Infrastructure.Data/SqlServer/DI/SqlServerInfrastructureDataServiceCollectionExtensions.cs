using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Context;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.DI
{
    /// <summary>
    /// Provides dependency injection helpers for the SQL Server external infrastructure data layer.
    ///
    /// PURPOSE:
    /// - Registers SQL Server-backed infrastructure services.
    /// - Supports both real EF-backed stores and deterministic in-memory stores.
    ///
    /// DESIGN:
    /// - SQL Server registration is isolated from other providers such as PostgreSQL or MongoDB.
    /// - The same store contracts can be backed either by EF Core or in-memory test data.
    ///
    /// IMPORTANT:
    /// - Use the SQL Server registration for real database access.
    /// - Use the in-memory registration for local development, CI, and GitHub tests.
    /// </summary>
    public static class SqlServerInfrastructureDataServiceCollectionExtensions
    {
        /// <summary>
        /// Adds SQL Server-backed external infrastructure data services.
        /// </summary>
        /// <param name="services">
        /// The service collection.
        /// </param>
        /// <param name="connectionString">
        /// The SQL Server connection string.
        /// </param>
        /// <returns>
        /// The same service collection for chaining.
        /// </returns>
        public static IServiceCollection AddExternalInfrastructureDataSqlServer(
            this IServiceCollection services,
            string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "Connection string cannot be null or whitespace.",
                    nameof(connectionString));
            }

            services.AddDbContext<ExternalSqlServerDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddScoped<ICandidateSqlServerStore, EfCandidateSqlServerStore>();
            services.AddScoped<IJobSqlServerStore, EfJobSqlServerStore>();

            return services;
        }

        /// <summary>
        /// Adds deterministic in-memory SQL Server-shaped infrastructure data services.
        /// </summary>
        /// <param name="services">
        /// The service collection.
        /// </param>
        /// <param name="candidateData">
        /// Preloaded candidate rows keyed by candidate identifier.
        /// </param>
        /// <param name="jobData">
        /// Preloaded job rows keyed by job identifier.
        /// </param>
        /// <returns>
        /// The same service collection for chaining.
        /// </returns>
        public static IServiceCollection AddExternalInfrastructureDataSqlServerInMemory(
            this IServiceCollection services,
            IReadOnlyDictionary<string, IReadOnlyList<CandidateSqlServerEntity>> candidateData,
            IReadOnlyDictionary<string, IReadOnlyList<JobSqlServerEntity>> jobData)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(candidateData);
            ArgumentNullException.ThrowIfNull(jobData);

            services.AddSingleton<ICandidateSqlServerStore>(
                new InMemoryCandidateSqlServerStore(candidateData));

            services.AddSingleton<IJobSqlServerStore>(
                new InMemoryJobSqlServerStore(jobData));

            return services;
        }
    }
}