using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores
{
    /// <summary>
    /// In-memory implementation of <see cref="IJobSqlServerStore"/>.
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
    public sealed class InMemoryJobSqlServerStore : IJobSqlServerStore
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<JobSqlServerEntity>> _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryJobSqlServerStore"/> class
        /// using the default seed dataset.
        /// </summary>
        public InMemoryJobSqlServerStore()
            : this(CreateDefaultSeed())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryJobSqlServerStore"/> class
        /// using the specified dataset.
        /// </summary>
        /// <param name="data">
        /// The in-memory dataset keyed by job identifier.
        /// </param>
        public InMemoryJobSqlServerStore(
            IReadOnlyDictionary<string, IReadOnlyList<JobSqlServerEntity>> data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<JobSqlServerEntity>> ReadByIdAsync(
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

            return Task.FromResult<IReadOnlyList<JobSqlServerEntity>>(
                Array.Empty<JobSqlServerEntity>());
        }

        /// <summary>
        /// Creates the default deterministic in-memory seed dataset.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyList<JobSqlServerEntity>> CreateDefaultSeed()
        {
            return new Dictionary<string, IReadOnlyList<JobSqlServerEntity>>(StringComparer.Ordinal)
            {
                ["job-001"] = new List<JobSqlServerEntity>
                {
                    new()
                    {
                        Id = "job-001",
                        Title = "Staff Software Engineer",
                        Description = "Lead backend architecture and distributed systems design.",
                    }
                },
                ["job-002"] = new List<JobSqlServerEntity>
                {
                    new()
                    {
                        Id = "job-002",
                        Title = "Senior Backend Engineer",
                        Description = "Build and maintain high-scale APIs and data workflows.",
                    }
                },
                ["job-003"] = new List<JobSqlServerEntity>
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