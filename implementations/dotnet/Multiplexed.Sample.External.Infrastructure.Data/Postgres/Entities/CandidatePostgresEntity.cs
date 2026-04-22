namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities
{
    /// <summary>
    /// Represents a PostgreSQL-backed candidate persistence model.
    /// </summary>
    public sealed class CandidatePostgresEntity
    {
        public string Id { get; set; } = default!;

        public string Name { get; set; } = default!;

        public string? Title { get; set; }

        public string? Skills { get; set; }
    }
}