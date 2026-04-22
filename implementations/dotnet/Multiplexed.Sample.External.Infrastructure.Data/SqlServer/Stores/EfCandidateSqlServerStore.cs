using Microsoft.EntityFrameworkCore;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Context;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores
{
    /// <summary>
    /// Entity Framework implementation of <see cref="ICandidateSqlServerStore"/>.
    ///
    /// PURPOSE:
    /// - Reads candidate data from SQL Server using EF Core.
    ///
    /// DESIGN:
    /// - Depends on <see cref="ExternalSqlServerDbContext"/>.
    /// - Returns provider-specific entities.
    ///
    /// IMPORTANT:
    /// - Must remain deterministic for the same query.
    /// </summary>
    public sealed class EfCandidateSqlServerStore : ICandidateSqlServerStore
    {
        private readonly ExternalSqlServerDbContext _dbContext;

        public EfCandidateSqlServerStore(ExternalSqlServerDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<CandidateSqlServerEntity>> ReadByIdAsync(
            string candidateId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                throw new ArgumentException("Candidate id cannot be null or whitespace.", nameof(candidateId));
            }

            return await _dbContext.Candidates
                .AsNoTracking()
                .Where(x => x.Id == candidateId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}