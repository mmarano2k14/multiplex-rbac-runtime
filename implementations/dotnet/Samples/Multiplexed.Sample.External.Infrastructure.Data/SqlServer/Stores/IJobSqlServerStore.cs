using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores
{
    /// <summary>
    /// Provides SQL Server-shaped job data for the external infrastructure layer.
    /// </summary>
    public interface IJobSqlServerStore
    {
        /// <summary>
        /// Reads job records by identifier.
        /// </summary>
        Task<IReadOnlyList<JobSqlServerEntity>> ReadByIdAsync(
            string jobId,
            CancellationToken cancellationToken = default);
    }
}