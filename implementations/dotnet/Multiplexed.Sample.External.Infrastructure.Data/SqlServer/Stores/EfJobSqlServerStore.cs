using Microsoft.EntityFrameworkCore;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Context;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores
{
    /// <summary>
    /// Entity Framework implementation of <see cref="IJobSqlServerStore"/>.
    ///
    /// PURPOSE:
    /// - Reads job data from SQL Server using EF Core.
    ///
    /// DESIGN:
    /// - Depends on <see cref="ExternalSqlServerDbContext"/>.
    /// - Returns provider-specific entities.
    ///
    /// IMPORTANT:
    /// - Must remain deterministic for the same query.
    /// </summary>
    public sealed class EfJobSqlServerStore : IJobSqlServerStore
    {
        private readonly ExternalSqlServerDbContext _dbContext;

        public EfJobSqlServerStore(ExternalSqlServerDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<JobSqlServerEntity>> ReadByIdAsync(
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