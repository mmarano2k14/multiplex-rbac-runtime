using Microsoft.EntityFrameworkCore;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Context;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores
{
    public sealed class EfJobPostgresStore : IJobPostgresStore
    {
        private readonly ExternalPostgresDbContext _dbContext;

        public EfJobPostgresStore(ExternalPostgresDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<JobPostgresEntity>> ReadByIdAsync(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job id cannot be null or whitespace.", nameof(jobId));
            }

            return await _dbContext.Jobs
                .AsNoTracking()
                .Where(x => x.Id == jobId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}