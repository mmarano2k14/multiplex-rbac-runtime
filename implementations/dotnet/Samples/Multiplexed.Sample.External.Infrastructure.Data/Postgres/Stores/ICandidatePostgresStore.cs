using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores
{
    public interface ICandidatePostgresStore
    {
        Task<IReadOnlyList<CandidatePostgresEntity>> ReadByIdAsync(
            string candidateId,
            CancellationToken cancellationToken = default);
    }
}