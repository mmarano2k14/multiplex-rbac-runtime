using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores
{
    public sealed class InMemoryCandidatePostgresStore : ICandidatePostgresStore
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<CandidatePostgresEntity>> _data;

        public InMemoryCandidatePostgresStore()
            : this(CreateDefaultSeed())
        {
        }

        public InMemoryCandidatePostgresStore(
            IReadOnlyDictionary<string, IReadOnlyList<CandidatePostgresEntity>> data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public Task<IReadOnlyList<CandidatePostgresEntity>> ReadByIdAsync(
            string candidateId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                throw new ArgumentException("Candidate id cannot be null or whitespace.", nameof(candidateId));
            }

            if (_data.TryGetValue(candidateId, out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult<IReadOnlyList<CandidatePostgresEntity>>(
                Array.Empty<CandidatePostgresEntity>());
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<CandidatePostgresEntity>> CreateDefaultSeed()
        {
            return new Dictionary<string, IReadOnlyList<CandidatePostgresEntity>>(StringComparer.Ordinal)
            {
                ["cand-101"] = new List<CandidatePostgresEntity>
                {
                    new()
                    {
                        Id = "cand-101",
                        Name = "Postgres Candidate",
                        Title = "Senior Backend Engineer",
                        Skills = "PostgreSQL, C#, Distributed Systems"
                    }
                }
            };
        }
    }
}