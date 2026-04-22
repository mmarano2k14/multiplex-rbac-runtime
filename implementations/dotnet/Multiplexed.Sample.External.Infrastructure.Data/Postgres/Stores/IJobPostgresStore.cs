using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores
{
    public interface IJobPostgresStore
    {
        Task<IReadOnlyList<JobPostgresEntity>> ReadByIdAsync(
            string jobId,
            CancellationToken cancellationToken = default);
    }
}