namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities
{
    /// <summary>
    /// Represents a SQL Server-backed candidate persistence model.
    ///
    /// PURPOSE:
    /// - Defines the database schema mapping for candidates in SQL Server.
    /// - Keeps SQL Server-specific persistence isolated from other providers.
    ///
    /// IMPORTANT:
    /// - This entity is provider-specific and must not be shared with other backends.
    /// </summary>
    public sealed class CandidateSqlServerEntity
    {
        /// <summary>
        /// Gets or sets the unique candidate identifier.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the candidate display name.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the candidate job title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the candidate skills description.
        /// </summary>
        public string? Skills { get; set; }

        /// <summary>
        /// Gets or sets the candidate resume rendered as HTML.
        /// </summary>
        public string? HtmlResume { get; set; }
    }
}