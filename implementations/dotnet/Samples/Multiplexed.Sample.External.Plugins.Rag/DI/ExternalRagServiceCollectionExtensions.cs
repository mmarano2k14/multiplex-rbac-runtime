using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.AI.Runtime.AI.Rag.Providers.Connectors;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Context;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Context;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores;
using Multiplexed.Sample.External.Plugins.Rag.Candidate.Postgres;
using Multiplexed.Sample.External.Plugins.Rag.Candidate.SqlServer;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.Postgres;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.Resolvers;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.SqlServer;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Postgres;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Resolvers;
using Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.SqlServer;
using Multiplexed.Sample.External.Plugins.Rag.Job.Postgres;
using Multiplexed.Sample.External.Plugins.Rag.Queries.Job.SqlServer;


namespace Multiplexed.Sample.External.Plugins.Rag.DI
{
    /// <summary>
    /// Global DI registration for external RAG providers (SQL Server + PostgreSQL).
    ///
    /// PURPOSE:
    /// - Registers all data providers (InMemory + EF).
    /// - Registers all RAG datasources.
    /// - Registers resolvers for multi-provider support.
    ///
    /// DESIGN:
    /// - Supports simultaneous providers (SQL + Postgres).
    /// - Provider selection is dynamic via providerKey.
    ///
    /// IMPORTANT:
    /// - Do not mix InMemory + EF for the same provider unless intentional.
    /// - Requires runtime connectors (AddMultiplexAI).
    /// </summary>
    public static class ExternalRagGlobalServiceCollectionExtensions
    {
        // ============================================================
        // SQL SERVER - IN MEMORY
        // ============================================================

        public static IServiceCollection AddExternalSqlServerInMemory(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<ICandidateSqlServerStore, InMemoryCandidateSqlServerStore>();
            services.AddSingleton<IJobSqlServerStore, InMemoryJobSqlServerStore>();

            return services;
        }

        // ============================================================
        // SQL SERVER - EF
        // ============================================================

        public static IServiceCollection AddExternalSqlServerEf(
            this IServiceCollection services,
            string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Invalid SQL Server connection string.", nameof(connectionString));

            services.AddDbContext<ExternalSqlServerDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddScoped<ICandidateSqlServerStore, EfCandidateSqlServerStore>();
            services.AddScoped<IJobSqlServerStore, EfJobSqlServerStore>();

            return services;
        }

        // ============================================================
        // POSTGRES - IN MEMORY
        // ============================================================

        public static IServiceCollection AddExternalPostgresInMemory(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<ICandidatePostgresStore, InMemoryCandidatePostgresStore>();
            services.AddSingleton<IJobPostgresStore, InMemoryJobPostgresStore>();

            return services;
        }

        // ============================================================
        // POSTGRES - EF
        // ============================================================

        public static IServiceCollection AddExternalPostgresEf(
            this IServiceCollection services,
            string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Invalid PostgreSQL connection string.", nameof(connectionString));

            services.AddDbContext<ExternalPostgresDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<ICandidatePostgresStore, EfCandidatePostgresStore>();
            services.AddScoped<IJobPostgresStore, EfJobPostgresStore>();

            return services;
        }

        // ============================================================
        // RAG LAYER (COMMON)
        // ============================================================

        public static IServiceCollection AddExternalRag(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Candidate datasources
            services.AddTransient<CandidateSqlServerRagDataSource>();
            services.AddTransient<CandidatePostgresRagDataSource>();

            // Job datasources
            services.AddTransient<JobSqlServerRagDataSource>();
            services.AddTransient<JobPostgresRagDataSource>();

            // Provider queries for runtime connectors
            services.AddTransient<IRelationalRagQuery, CandidateSqlServerRelationalRagQuery>();
            services.AddTransient<IRelationalRagQuery, JobSqlServerRelationalRagQuery>();
            services.AddTransient<IRelationalRagQuery, CandidatePostgresRelationalRagQuery>();
            services.AddTransient<IRelationalRagQuery, JobPostgresRelationalRagQuery>();

            // Resolvers
            services.AddTransient<ICandidateRagDataSourceResolver, DefaultCandidateRagDataSourceResolver>();
            services.AddTransient<IJobRagDataSourceResolver, DefaultJobRagDataSourceResolver>();

            return services;
        }
    }
}