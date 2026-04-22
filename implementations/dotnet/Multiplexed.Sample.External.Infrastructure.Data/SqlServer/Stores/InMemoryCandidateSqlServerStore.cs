using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores
{
    /// <summary>
    /// In-memory implementation of <see cref="ICandidateSqlServerStore"/>.
    ///
    /// PURPOSE:
    /// - Supports local development, CI, and GitHub test scenarios.
    /// - Avoids requiring a live SQL Server dependency.
    ///
    /// DESIGN:
    /// - Uses a deterministic in-memory dataset.
    /// - Provides a default seed when no dataset is supplied.
    ///
    /// IMPORTANT:
    /// - Intended for testing and sample scenarios only.
    /// </summary>
    public sealed class InMemoryCandidateSqlServerStore : ICandidateSqlServerStore
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<CandidateSqlServerEntity>> _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryCandidateSqlServerStore"/> class
        /// using the default seed dataset.
        /// </summary>
        public InMemoryCandidateSqlServerStore()
            : this(CreateDefaultSeed())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryCandidateSqlServerStore"/> class
        /// using the specified dataset.
        /// </summary>
        /// <param name="data">
        /// The in-memory dataset keyed by candidate identifier.
        /// </param>
        public InMemoryCandidateSqlServerStore(
            IReadOnlyDictionary<string, IReadOnlyList<CandidateSqlServerEntity>> data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<CandidateSqlServerEntity>> ReadByIdAsync(
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

            return Task.FromResult<IReadOnlyList<CandidateSqlServerEntity>>(
                Array.Empty<CandidateSqlServerEntity>());
        }

        /// <summary>
        /// Creates the default deterministic in-memory seed dataset.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyList<CandidateSqlServerEntity>> CreateDefaultSeed()
        {
            return new Dictionary<string, IReadOnlyList<CandidateSqlServerEntity>>(StringComparer.Ordinal)
            {
                ["cand-001"] = new List<CandidateSqlServerEntity>
                {
                    new()
                    {
                        Id = "cand-001",
                        Name = "Marco Marano",
                        Title = "Staff Software Engineer",
                        Skills = "C#, .NET, Distributed Systems, Redis, MongoDB, Angular"
                    }
                },
                ["cand-002"] = new List<CandidateSqlServerEntity>
                {
                    new()
                    {
                        Id = "cand-002",
                        Name = "Alice Dupont",
                        Title = "Senior Backend Engineer",
                        Skills = "C#, ASP.NET Core, SQL Server, RabbitMQ, Clean Architecture"
                    }
                },
                ["cand-003"] = new List<CandidateSqlServerEntity>
                {
                    new()
                    {
                        Id = "cand-003",
                        Name = "Ben Carter",
                        Title = "Platform Engineer",
                        Skills = "Kubernetes, Terraform, AWS, CI/CD, Observability"
                    }
                }
            };
        }
    }
}