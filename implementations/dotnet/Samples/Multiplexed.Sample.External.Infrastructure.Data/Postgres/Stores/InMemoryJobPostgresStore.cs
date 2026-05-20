using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Stores
{
    public sealed class InMemoryJobPostgresStore : IJobPostgresStore
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<JobPostgresEntity>> _data;

        public InMemoryJobPostgresStore()
            : this(CreateDefaultSeed())
        {
        }

        public InMemoryJobPostgresStore(
            IReadOnlyDictionary<string, IReadOnlyList<JobPostgresEntity>> data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public Task<IReadOnlyList<JobPostgresEntity>> ReadByIdAsync(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job id cannot be null or whitespace.", nameof(jobId));
            }

            if (_data.TryGetValue(jobId, out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult<IReadOnlyList<JobPostgresEntity>>(
                Array.Empty<JobPostgresEntity>());
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<JobPostgresEntity>> CreateDefaultSeed()
        {
            return new Dictionary<string, IReadOnlyList<JobPostgresEntity>>(StringComparer.Ordinal)
            {
                ["job-001"] = new List<JobPostgresEntity>
                {
                    new()
                    {
                        Id = "job-001",
                        Title = "Staff Software Engineer",
                        Description = "Lead backend architecture and distributed systems design.",
                    }
                },
                ["job-002"] = new List<JobPostgresEntity>
                {
                    new()
                    {
                        Id = "job-002",
                        Title = "Senior Backend Engineer",
                        Description = "Build and maintain high-scale APIs and data workflows.",
                    }
                },
                ["job-003"] = new List<JobPostgresEntity>
                {
                    new()
                    {
                        Id = "job-003",
                        Title = "Platform Engineer",
                        Description = "Improve deployment systems, cloud infrastructure, and reliability.",
                    }
                }
            };
        }
    }
}