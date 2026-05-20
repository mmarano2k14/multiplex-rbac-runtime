using System;
using Microsoft.EntityFrameworkCore;
using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Context
{
    /// <summary>
    /// SQL Server DbContext for the external infrastructure data layer.
    ///
    /// PURPOSE:
    /// - Provides SQL Server-backed persistence access for external data.
    /// - Isolates SQL Server infrastructure from runtime and plugin layers.
    ///
    /// DESIGN:
    /// - Contains only SQL Server-specific entities.DI
    /// - Supports deterministic queries through EF Core.
    ///
    /// IMPORTANT:
    /// - Must not be reused for PostgreSQL or MongoDB.
    /// </summary>
    public sealed class ExternalSqlServerDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalSqlServerDbContext"/> class.
        /// </summary>
        public ExternalSqlServerDbContext(
            DbContextOptions<ExternalSqlServerDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets the SQL Server candidate records.
        /// </summary>
        public DbSet<CandidateSqlServerEntity> Candidates => Set<CandidateSqlServerEntity>();

        /// <summary>
        /// Gets the SQL Server job records.
        /// </summary>
        public DbSet<JobSqlServerEntity> Jobs => Set<JobSqlServerEntity>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder);

            ConfigureCandidate(modelBuilder);
            ConfigureJob(modelBuilder);
        }

        /// <summary>
        /// Configures the Candidate entity mapping.
        /// </summary>
        private static void ConfigureCandidate(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<CandidateSqlServerEntity>();

            entity.ToTable("Candidates");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.TenantId)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.Title)
                .HasMaxLength(256);

            entity.Property(x => x.Skills)
                .HasMaxLength(4000);

            entity.Property(x => x.HtmlResume)
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => x.TenantId);
        }

        /// <summary>
        /// Configures the Job entity mapping.
        /// </summary>
        private static void ConfigureJob(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<JobSqlServerEntity>();

            entity.ToTable("Jobs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.TenantId)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.Description)
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => x.TenantId);
        }
    }
}