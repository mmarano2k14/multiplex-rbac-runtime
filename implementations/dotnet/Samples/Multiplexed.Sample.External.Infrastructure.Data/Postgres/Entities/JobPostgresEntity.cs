namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities
{
    /// <summary>
    /// Represents a PostgreSQL-backed job persistence model.
    /// </summary>
    public sealed class JobPostgresEntity
    {
        public string Id { get; set; } = default!;

        public string Title { get; set; } = default!;

        public string? Description { get; set; }

        public string? Requirements { get; set; }
    }
}