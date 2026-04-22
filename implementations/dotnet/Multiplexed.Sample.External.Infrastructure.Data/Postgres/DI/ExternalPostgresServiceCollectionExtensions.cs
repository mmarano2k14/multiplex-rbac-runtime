using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Context;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.DI
{
    public static class ExternalPostgresServiceCollectionExtensions
    {
        public static IServiceCollection AddExternalPostgresInMemory(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<ICandidatePostgresStore, InMemoryCandidatePostgresStore>();
            services.AddSingleton<IJobPostgresStore, InMemoryJobPostgresStore>();

            return services;
        }

        public static IServiceCollection AddExternalPostgresEf(
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

            services.AddDbContext<ExternalPostgresDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<ICandidatePostgresStore, EfCandidatePostgresStore>();
            services.AddScoped<IJobPostgresStore, EfJobPostgresStore>();

            return services;
        }
    }
}