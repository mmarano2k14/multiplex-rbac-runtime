using Microsoft.EntityFrameworkCore;
using Multiplexed.Sample.External.Infrastructure.Data.Postgres.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.Postgres.Context
{
    /// <summary>
    /// PostgreSQL DbContext for the external infrastructure data layer.
    /// </summary>
    public sealed class ExternalPostgresDbContext : DbContext
    {
        public ExternalPostgresDbContext(
            DbContextOptions<ExternalPostgresDbContext> options)
            : base(options)
        {
        }

        public DbSet<CandidatePostgresEntity> Candidates => Set<CandidatePostgresEntity>();

        public DbSet<JobPostgresEntity> Jobs => Set<JobPostgresEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder);

            ConfigureCandidate(modelBuilder);
            ConfigureJob(modelBuilder);
        }

        private static void ConfigureCandidate(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<CandidatePostgresEntity>();

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.Title)
                .HasMaxLength(256);

            entity.Property(x => x.Skills)
                .HasMaxLength(4000);
        }

        private static void ConfigureJob(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<JobPostgresEntity>();

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.Description)
                .HasMaxLength(4000);

            entity.Property(x => x.Requirements)
                .HasMaxLength(4000);
        }
    }
}