using Microsoft.EntityFrameworkCore;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Context;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores
{
    public sealed class EfCandidatePostgresStore : ICandidatePostgresStore
    {
        private readonly ExternalPostgresDbContext _dbContext;

        public EfCandidatePostgresStore(ExternalPostgresDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<CandidatePostgresEntity>> ReadByIdAsync(
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